#nullable disable
using CallFlow;
using System;
using System.Threading;
using System.Threading.Tasks;
using TCX.Configuration;
using TCX.PBXAPI;
using System.Collections.Generic;

namespace dummy
{
    //PIN IVR
    //when call is coming to the IVR it asks for user PIN and deliver call to the "pined" destination
    public class UserInputIVRSample : ScriptBase<UserInputIVRSample>
    {
        ///DTMF input events hanlder.
        ///<see cref="getUserInput"/> 
        void OnDTMFInput(char x, TaskCompletionSource<string> currentInputTaskSource, ref string theInput, Timer timer, int interdigits)
        {
            if(x=='#')
            {
                //we deactivate timer (if it supplied
                //timer is null only if the input is completed by timer itself and we don't need to reset it,
                timer?.Change(Timeout.Infinite, Timeout.Infinite);
                //put current input to the log fro debugging purpose
                MyCall.Info($"User input is '{theInput}'");
                //we are trying to set result. If the task is already completed, we don't care.
                currentInputTaskSource.TrySetResult(theInput);
            }
            else
            {
                //we prolongue the timer elapsing
                timer?.Change(interdigits, Timeout.Infinite);
                //and add coming char to the collecting string
                theInput += x;
            }
        }
        
        //definitiona of PIN codes
        //Key - the PIN
        //Value - the destination
        Dictionary<string, string> UserInputMap=new Dictionary<string, string>
        {
            {"1234", "8001"},
            {"2345", "8002"},
            {"3456", "0000"},
            {"", "0001"}      //NO INPUT
        };
        
        //Procedure which play the prompt and returns caller input.
        //caller can finish own input by pressing #
        //otherwise, after inputtimeout(no input), or interdigitTimeOut (if there was the caller input) the task will be completed,
        async Task<string> getUserInput(int interdigitTimeOut, int inputtimeout)
        {
            //it is the collecting string. OnDTMFInput (see above) will collect caller's input here.
            string theInput=""; 
            //it is our task completion source which will be completed by OnDTMFInput when caller(or timer) will finish the input
            var currentInputTaskSource = new TaskCompletionSource<string>();
            //it is the timer which will trigger "end of input" when elapses.
            var timer = new Timer((x)=>OnDTMFInput('#', currentInputTaskSource, ref theInput, null, 0), null, inputtimeout, Timeout.Infinite);
            //this ICall.OnDTMFInput event handler we will set for this caller input task
            var x = (char x)=>OnDTMFInput(x, currentInputTaskSource, ref theInput, timer, interdigitTimeOut);
            MyCall.OnDTMFInput+=x;
            try
            {
                //We reset input buffer and playing prompt. Prompt will be cancelled when caller will start input.
                await MyCall.PlayPrompt(null, ["PLSENTER", "PIN", "THENPRESPND"], PlayPromptOptions.ResetBufferAtStart|PlayPromptOptions.CancelPlaybackAtFirstChar);
                MyCall.Info($"prompt played"); //
                return await currentInputTaskSource.Task;
            }
            finally
            {
                //we are disposing timer and remove even handler of this task.
                timer.Dispose();
                MyCall.OnDTMFInput -= x;
            }
        }


        //we are handling call as detached task.
        public override async void Start()
        {
            try
            {
                await Task.Run(async () =>
                {
                    try
                    {
                        //Reporting delay of the task execution.
                        MyCall.Debug($"Script start delay: {DateTime.UtcNow - MyCall.LastChangeStatus}");
                        //reports the connection which is handled by the script
                        MyCall.Debug($"Incoming connection {MyCall}");
                        //we need access to the PBX configuration
                        var ps = MyCall.PS as PhoneSystem;
                        CallControlResult lastresult = null;
                        bool scriptCompleted = false;
                        //three tries to reach the destination
                        int count=3;
                        //We need to interact with caller, So, we assure that media connection is there.
                        //We set default music on hold as backgroud music. Prompt will overlap it.
                        if(await MyCall.AssureMedia().ContinueWith(x=>{ MyCall.SetBackgroundAudio(true, [ps.GetParameterValue("MUSICONHOLDFILE")]);return x.Result; }))
                        {
                            //we give caller three attempts to enter PIN. 
                            //in case of successful delivery - script ends.
                            //ExecutionMode==ScriptExecutionMode.Active is check that we are still connected with call.
                            //the execution mode will be switched to ScriptExecutionMode.Wrapup when the script will be disconnected from call 
                            while(count-->0 && ExecutionMode==ScriptExecutionMode.Active)
                            {
                                string userInput = null;
                                try
                                {
                                    //the main script action. 
                                    //get user input with specified interdigit timeout (or no input timeout)
                                    //take corresponding destination from UserInputMap (see above)
                                    //Delivery is using "RouteToAsync" method and script is still interacts with caller while
                                    //core delivers call.
                                    //the action is just a chain of tasks
                                    //getUserInput Continued with RouteToAsync
                                    //the exceptions are generated if call is not delivered to the new destination(user input don't much the defined destinations
                                    //script executes another iteration if current iteration was failed.
                                    lastresult = await getUserInput(2000, 10000)
                                        .ContinueWith(x=>
                                        {
                                            userInput = x.Result;
                                            return MyCall.RouteToAsync(new DestinationStruct(ps.GetDNByNumber(UserInputMap[userInput])));
                                        }).Unwrap()
                                        .ContinueWith(x=>x.Result, TaskContinuationOptions.OnlyOnRanToCompletion);
                                    //successfuly delivered. we terminate loop
                                    scriptCompleted = true; 
                                    break;
                                }
                                catch (PBXIsNotConnected ex)
                                {
                                    //this exaception means that the core is not reachable at the moment.
                                    MyCall.Error($"Call control API service is not available at the moment:\n{ex}");
                                }
                                catch(OperationFailed ex)
                                {
                                    //this exaception is generated by CallControlAPI requests in case if RouteToAsync request has not delivered the call to destination
                                    //it is recommended to check real reason. For example, reason CompletedElsewhere. PartyConnectionLost or ParentConnectionTerminated
                                    //mean that the script will not be able to continue because script was disconnected from the call, ot caller has left the call (has dropped the call)
                                    MyCall.Error($"Failed to deliver the call - {userInput}={ex}");
                                }
                                catch (TaskCanceledException ex)
                                {
                                    //the task was interrupted (rejected) by the core. Core rejected the request. The request was not accepted by the core.
                                    //we can try to continue.
                                    MyCall.Trace($"Task was cancelled: {ex}");
                                }
                                catch (Exception ex)
                                {
                                    //other exceptions are related to the "script lofic failures"
                                    //we can try to repeat.
                                    MyCall.Error($"Script logic exception:{ex}");
                                }
                            }
                        }
                        //we are here because:
                        //1. call was successfuly delivered (scriptCompleted==true)
                        //2. call was not delivered after three attemts
                        MyCall.Info("UserInputIVRSample Exit script");
                        //Exit from script.
                        //Other sctips may CALL this script in context of the own connection (delagate handling and wait for the result)
                        //In such case, MyCall.Return(scriptCompleted); will return control to the calling script.
                        //the convention is:
                        //- return true, if script successfuly reaches the own goal.
                        //- return false, if script was not able to process the call.
                        //The goal of this script is delivery to the destination. We return scriptCompleted
                        //which is set ot true when call was delivered.
                        MyCall.Return(scriptCompleted);
                    }
                    catch //task must not generate exceptions
                    {
                        MyCall.Return(false);
                    }
                    
                });
            }
            catch //task must not generate exceptions
            {
                MyCall.Return(false);
            }
        }
    }
}