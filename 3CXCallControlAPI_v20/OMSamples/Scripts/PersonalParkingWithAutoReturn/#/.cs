#nullable disable
using CallFlow;
using System;
using System.Threading;
using System.Threading.Tasks;
using TCX.Configuration;
using TCX.PBXAPI;
namespace dummy
{
    public class ParkingRoutePointSample : ScriptBase<ParkingRoutePointSample>
    {
        async Task<CallControlResult> ProcessAutoPickup(RoutePoint sp, DestinationStruct returnTo, CancellationToken token)
        {
            while (true)
                try
                {
                    return await Task.Delay(TimeSpan.FromSeconds(15), token).ContinueWith(x =>
                    {
                        MyCall.Trace("{0} - automatic redirection of the call from {1}.{2} to '{3}'", MyCall.DN, MyCall.Caller?.CallerID, MyCall.Caller?.DN, returnTo);
                        return MyCall.RouteToAsync(new RouteRequest
                        {
                            RouteTarget = returnTo,
                            TimeOut = TimeSpan.FromSeconds(15) //will ring until failure
                        }
                        );
                    }
                    , TaskContinuationOptions.NotOnCanceled).Unwrap();
                }
                catch (OperationFailed ex)
                {
                    MyCall.Trace("Automatic redirection failed: {0}", ex.TheResult);
                    MyCall.Trace("Continue hold call from {0}({1}) on {2}", MyCall.Caller?.CallerID, MyCall.Caller?.DN, MyCall.DN);
                    continue;
                }
        }
        PhoneSystem ps = null;  

        public override async void Start()
        {
            await Task.Run(async () =>
            {
                try
                {
                    MyCall.Debug($"Script start delay: {DateTime.UtcNow - MyCall.LastChangeStatus}");
                    MyCall.Debug($"Incoming connection {MyCall}");
                    ps = MyCall.PS as PhoneSystem;
                    CallControlResult lastresult = null;
                    DN referredBy = null;
                    RoutePoint thisPark = null;
                    string callerID = "";
                    DN callerDN = null;
                    bool scriptCompleted = true;
                    try
                    {
                        referredBy = MyCall.ReferredByDN?.GetFullSnapshot() as Extension;
                        thisPark = MyCall.DN?.Clone() as RoutePoint;
                        callerID = MyCall.Caller?.CallerID;
                        callerDN = MyCall.Caller?.DN?.Clone() as DN;
                        MyCall.Trace(
                            "Parked call from {0}({1}) on {2}", callerID, callerDN, thisPark
                        );
                        if (referredBy == null)
                        {
                            MyCall.Trace("{0} rejects call from {1}. Reason: No referrer specified", thisPark, callerDN);
                            return;
                        }
                        var cancelationToken = new CancellationTokenSource();
                        MyCall.OnTerminated += () =>
                        {
                            cancelationToken.Cancel();
                        };
                        lastresult = await MyCall.AssureMedia().ContinueWith(
                            x =>
                            {
                                if(!string.IsNullOrWhiteSpace(ps.GetParameterValue("PARK_MOH_SOURCE")))
                                    MyCall.SetBackgroundAudio(true, new string[] { ps.GetParameterValue("PARK_MOH_SOURCE") });
                                else
                                    MyCall.SetBackgroundAudio(true, new string[] { ps.GetParameterValue("MUSICONHOLDFILE") });
                                return ProcessAutoPickup(thisPark, new DestinationStruct(referredBy), cancelationToken.Token);
                            }, TaskContinuationOptions.OnlyOnRanToCompletion).Unwrap();
                    }
                    catch (PBXIsNotConnected ex)
                    {
                        MyCall.Error($"Call control API is not available:\n{ex}");
                        scriptCompleted = false;
                    }
                    catch (TaskCanceledException)
                    {
                        MyCall.Trace($"Call was disconnected from parking place");
                    }
                    catch (Exception ex)
                    {
                        MyCall.Error($"Parking failure:\n{ex}");
                        scriptCompleted = false;
                    }
                    finally
                    {
                        try
                        {
                            MyCall.Info("Call from {0}({1}) parked by {2} on {3} finished with result={4}", callerID, callerDN, referredBy, thisPark, lastresult?.ToString() ?? "terminated");
                        }
                        catch (Exception ex)
                        {
                            MyCall.Error($"SharedParkingFlow finalize exception {ex}");
                        }
                        MyCall.Return(scriptCompleted);
                    }
                }
                catch
                {
                    MyCall.Return(false);
                }
            });
        }
    }
}

