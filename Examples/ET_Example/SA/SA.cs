using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NationalInstruments.ModularInstruments.NIRfsa;
using NationalInstruments.RFmx.InstrMX;
using NationalInstruments.RFmx.NRMX;
using NationalInstruments;

namespace SA
{
    public class SA
    {
        RFmxNRMX NR;
        RFmxInstrMX instrSession;
        public double[] lowerRelativePower;                                               /* (dB) */
        public double[] upperRelativePower;                                               /* (dB) */
        public double[] lowerAbsolutePower;                                               /* (dBm) */
        public double[] upperAbsolutePower;                                               /* (dBm) */
        public double absolutePower;
        public double relativePower;

        public SA(RFmxInstrMX instSession)
        {
            NR = instSession.GetNRSignalConfiguration();     /* Create a new RFmx Session */
            instrSession = instSession;
            instrSession.ConfigureFrequencyReference("", RFmxInstrMXConstants.PxiClock, 10.0e6);
        }

        public void ConfigureSA(double ReferenceLevel, double CarrierFrequency)
        {
            NR.SetSelectedPorts("", "if1");
            NR.ConfigureFrequency("", CarrierFrequency);
            NR.ConfigureExternalAttenuation("", 0);
            //NR.ConfigureIQPowerEdgeTrigger("", "0", RFmxNRMXIQPowerEdgeTriggerSlope.Rising, -20, 0, RFmxNRMXTriggerMinimumQuietTimeMode.Auto, 0, RFmxNRMXIQPowerEdgeTriggerLevelType.Relative, true);
            NR.ConfigureDigitalEdgeTrigger("", RFmxInstrMXConstants.PxiTriggerLine1, RFmxNRMXDigitalEdgeTriggerEdge.Rising, 0, true);
            NR.ComponentCarrier.SetBandwidth("", 100e6);
            NR.ComponentCarrier.SetBandwidthPartSubcarrierSpacing("", 30e3);
            NR.ConfigureReferenceLevel("", ReferenceLevel);
            NR.SelectMeasurements("", RFmxNRMXMeasurementTypes.Acp, true);
            NR.Acp.Configuration.ConfigureMeasurementMethod("", RFmxNRMXAcpMeasurementMethod.Normal);
            NR.Acp.Configuration.ConfigureNoiseCompensationEnabled("", RFmxNRMXAcpNoiseCompensationEnabled.False);
            NR.Acp.Configuration.ConfigureSweepTime("", RFmxNRMXAcpSweepTimeAuto.False, 1e-3);
            NR.Acp.Configuration.ConfigureNumberOfUtraOffsets("", 0);
            NR.Acp.Configuration.ConfigureNumberOfEutraOffsets("", 0);
        }

        public void InitiateSA(string resultName, bool wait = false)
        {
            if (wait) instrSession.WaitForAcquisitionComplete(10);
            NR.Initiate("", resultName);
        }

        public void WaitForSAComplete()
        {
            instrSession.WaitForAcquisitionComplete(10);
        }

        public void FetchAcpRecord(string resultName)
        {
            try
            {
                string resultString = RFmxNRMX.BuildResultString(resultName);
                NR.Acp.Results.FetchOffsetMeasurementArray(resultString, 20, ref lowerRelativePower,
                   ref upperRelativePower, ref lowerAbsolutePower, ref upperAbsolutePower);

                NR.Acp.Results.ComponentCarrier.FetchMeasurement(resultString, 20, out absolutePower, out relativePower);
            }

            //Sometimes the analysis thread runs ahead of the acquisition, we can resolve this by waiting for a few miliseconds and trying again 
            catch (RFmxException e) when (e.Message.Contains("-380405"))
            {
                string resultString = RFmxNRMX.BuildResultString(resultName);
                NR.Acp.Results.FetchOffsetMeasurementArray(resultString, 20, ref lowerRelativePower,
                   ref upperRelativePower, ref lowerAbsolutePower, ref upperAbsolutePower);

                NR.Acp.Results.ComponentCarrier.FetchMeasurement(resultString, 20, out absolutePower, out relativePower);
            }
        }
        public void CloseSASession()
        {
            if (NR != null)
            {
                NR.Dispose();
                NR = null;
            }
            if (instrSession != null)
            {
                instrSession.Close();
                instrSession = null;
            }
        }
    }
}
