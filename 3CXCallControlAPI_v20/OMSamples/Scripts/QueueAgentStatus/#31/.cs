using System;
using System.Threading.Tasks;
using TCX.Configuration;
using CallFlow;

namespace dummy
{
    /// <summary>
    /// call to #30 logount from all queus and set logout status for the extension
    /// </summary>
    public class LogoutAndResetParticipation : ScriptBase<LogoutAndResetParticipation>
    {
        public override void Start()
        {
            Task.Run(async () =>
                {
                    try
                    {
                        if (MyCall.Caller.DN is Extension ext)
                        {
                            ext.QueueStatus = QueueStatusType.LoggedOut;
                            foreach (var qa in ext.QueueMembership)
                            {
                                qa.QueueStatus = QueueStatusType.LoggedOut;
                            }
                            ext.Save();
                            MyCall.Info($"Extension {ext.Number} has been logged out from all queues");
                            try
                            {
                                await MyCall.AssureMedia().ContinueWith(_ => MyCall.PlayPrompt(ext.GetPropertyValue("PROMPTSETID"), new[] { "LOGGED_OUT" }, PlayPromptOptions.Blocked)).Unwrap();
                            }
                            catch(Exception ex)
                            {
                                MyCall.Info($"Operation failed:{ex}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MyCall.Info($"Logout operation failed:{ex}");
                    }
                    finally
                    {
                        MyCall.Return(true);
                    }
                });
        }
    }
}
