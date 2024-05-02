#nullable disable
using CallFlow;
using System;
using System.Threading.Tasks;
using TCX.Configuration;
using System.Linq;
using CallFlow.CFD;
namespace dummy
{
    public class ForcePBXBreakHours : ScriptBase<ForcePBXBreakHours>
    {
        public override async void Start()
        {
            var scriptCompleted = false;
            try
            {
                await Task.Run(async () =>
                {
                    try
                    {
                        PhoneSystem ps = MyCall.PS as PhoneSystem;
                        //in this example, all groups will be overrides to the end of the day in "global" PBX timezone
                        var PBXLocalTimeEndOfOverride = DateTime.Now.Date+new TimeSpan(23, 59,59);
                        ps.GetAll<Group>().Where(x=>x.AllowCallService).Select(x=>
                        {
                            //WARNING: time check will be available in v20 SP1+
                            //This script will not be compilable in earlier versions
                            var groupCurrentTime = x.Now(out var utc, out var timezone, out var groupmode);
                            x.OverrideExpiresAt = TimeZoneInfo.ConvertTime(PBXLocalTimeEndOfOverride, timezone);
                            x.CurrentGroupHours = GroupHoursMode.ForceBreak;
                            MyCall.Info($"{x.Name} reset to '{x.CurrentGroupHours}' until {x.OverrideExpiresAt} @ time:utc={utc}, timezone={timezone}, groupmode={groupmode}");
                            return x;
                        }).OMSave();
                        var result = await MyCall.AssureMedia().ContinueWith(_ => MyCall.PlayPrompt(null, new[] { "Empty.wav", "ST_AWAY_SET" }, PlayPromptOptions.Blocked), TaskContinuationOptions.OnlyOnRanToCompletion).Unwrap();
                        MyCall.Return(true);
                    }
                    catch
                    {
                        MyCall.Error("Unexcpected failure");
                        scriptCompleted = false;
                    }
                    finally
                    {
                        MyCall.Return(scriptCompleted);
                    }
                });

           }
           catch
           {
               MyCall.Critical("Cannot execute call handler");
               MyCall.Return(false);
           }
        }
    }
}