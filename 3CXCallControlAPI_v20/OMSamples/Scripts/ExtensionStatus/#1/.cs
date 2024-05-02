using System;
using System.Threading.Tasks;
using TCX.Configuration;
using CallFlow;
using System.Linq;
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
                        ext.CurrentProfile = ext.FwdProfiles.First(x=>x.Name.Equals("Away"));
                        ext.Save();
                        MyCall.Info($"Extension {ext.Number} is set to {ext.CurrentProfile.Name} status");
                        try
                        {
                            await MyCall.AssureMedia().ContinueWith(_ => MyCall.PlayPrompt(ext.GetPropertyValue("PROMPTSETID"), new[] { "ST_AWAY_SET" }, PlayPromptOptions.Blocked)).Unwrap();
                        }
                        catch(Exception ex)
                        {
                            MyCall.Info($"Prompt playback failed:{ex}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    MyCall.Info($" failed:{ex}");
                }
                finally
                {
                    MyCall.Return(true);
                }
            });
        }
    }

}
