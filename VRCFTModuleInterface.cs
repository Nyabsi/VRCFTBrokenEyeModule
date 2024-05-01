using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using VRCFaceTracking;
using VRCFaceTracking.Core.Library;
using VRCFaceTracking.Core.Types;

namespace VRCFTBrokenEyeModule
{
    public class VRCFTBrokenEyeModule : ExtTrackingModule
    {
        private readonly Client beTcpClient = new();

        private double minValidPupilDiameterMm = 999f;
        private static SmoothFloat LeftLid = new();
        private static SmoothFloat RightLid = new();

        public override (bool SupportsEye, bool SupportsExpression) Supported => (true, false);

        public override (bool eyeSuccess, bool expressionSuccess) Initialize(bool eyeAvailable, bool expressionAvailable)
        {
            bool isConnected = false;

            Logger.LogInformation("Attempting to connect BrokenEye at 127.0.0.1:5555.");

            int i = 0;
            while (!isConnected)
            {
                if (beTcpClient.Connect())
                {
                    isConnected = true;
                    break;
                }

                if (i == 5)
                    break;

                i++;
                Logger.LogInformation($"Failed to connect to BrokenEye, re-trying after 3 seconds, attempt: {i}/5");
                Thread.Sleep(3000);
            }

            if (!isConnected)
            {
                Logger.LogInformation("Failed to establish connection after 5 tries, are you sure BrokenEye is running?");
            } 
            else
            {
                SmoothFloatWorkers.Init();
                Logger.LogInformation("Established connection to BrokenEye sucessfully, listening for data...");
            }

            ModuleInformation = new ModuleMetadata()
            {
                Name = "BrokenEye",
            };

            return (eyeAvailable && isConnected, false);
        }

        public override void Update()
        {
            if (Status != ModuleState.Active)
                return;

            (bool isValid, EyeData data) = beTcpClient.FetchData();

            if (isValid)
            {
                if (data.Left.OpennessIsValid)
                {
                    LeftLid.Value = data.Left.OpennessIsValid ? data.Left.Openness : 1f;
                    UnifiedTracking.Data.Eye.Left.Openness = LeftLid.Value;
                }

                if (data.Right.OpennessIsValid)
                {
                    RightLid.Value = data.Right.OpennessIsValid ? data.Right.Openness : 1f;
                    UnifiedTracking.Data.Eye.Right.Openness = RightLid.Value;
                }

                if (data.Left.PupilDiameterIsValid)
                    UnifiedTracking.Data.Eye.Left.PupilDiameter_MM = data.Left.PupilDiameterMm;

                if (data.Right.PupilDiameterIsValid)
                    UnifiedTracking.Data.Eye.Right.PupilDiameter_MM = data.Right.PupilDiameterMm;

                if (data.Left.GazeDirectionIsValid)
                    UnifiedTracking.Data.Eye.Left.Gaze = data.Left.GazeDirection.ToVRCFT().FlipXCoordinates();

                if (data.Right.GazeDirectionIsValid)
                    UnifiedTracking.Data.Eye.Right.Gaze = data.Right.GazeDirection.ToVRCFT().FlipXCoordinates();

                const float minPupilDiameterThreshold = 1f;

                if (data.Left.PupilDiameterIsValid && data.Right.PupilDiameterIsValid && data.Left.PupilDiameterMm > minPupilDiameterThreshold && data.Right.PupilDiameterMm > minPupilDiameterThreshold)
                {
                    minValidPupilDiameterMm = Math.Min(minValidPupilDiameterMm, (data.Left.PupilDiameterMm + data.Right.PupilDiameterMm) / 2.0);
                }

                if (data.Left.PupilDiameterIsValid || data.Right.PupilDiameterIsValid)
                {
                    UnifiedTracking.Data.Eye._minDilation = (float)minValidPupilDiameterMm;
                }
            }
        }

        public override void Teardown()
        {
            beTcpClient.Dispose();
            SmoothFloatWorkers.Destroy();
        }
    }

}