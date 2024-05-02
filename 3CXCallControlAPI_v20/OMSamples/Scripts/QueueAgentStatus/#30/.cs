using System;
using System.Threading.Tasks;
using TCX.Configuration;
using CallFlow;
namespace dummy
{
    /// <summary>
    /// call to #31 loging  in extension to all queues
    /// </summary>
    public class LoginToAllQueues : ScriptBase<LoginToAllQueues>
    {
        public override void Start()
        {
            Task.Run(async () =>
            {
                try
                {
                    if (MyCall.Caller.DN is Extension ext)
                    {
                        ext.QueueStatus = QueueStatusType.LoggedIn;
                        foreach (var qa in ext.QueueMembership)

                        {
                            qa.QueueStatus = QueueStatusType.LoggedIn;
                        }
                        ext.Save();
                        MyCall.Info($"Extension {ext.Number} has been logged in to all queues");
                        try
                        {
                            await MyCall.AssureMedia().ContinueWith(_ => MyCall.PlayPrompt(ext.GetPropertyValue("PROMPTSETID"), new[] { "LOGGED_IN" }, PlayPromptOptions.Blocked)).Unwrap();
                        }
                        catch(Exception ex)
                        {
                            MyCall.Info($"Prompt playback failed:{ex}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    MyCall.Info($"Login operation failed failed:{ex}");
                }
                finally
                {
                    MyCall.Return(true);
                }
            });
        }
    }

}
