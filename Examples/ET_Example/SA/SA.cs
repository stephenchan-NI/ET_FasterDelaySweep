using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NationalInstruments.ModularInstruments.NIRfsa;
using NationalInstruments.RFmx.InstrMX;
using NationalInstruments.RFmx.SpecAnMX;
using NationalInstruments;

namespace SA
{
    public class SA
    {
        RFmxSpecAnMX specAn;
        RFmxInstrMX instrSession;
        public double[] lowerRelativePower;                                               /* (dB) */
        public double[] upperRelativePower;                                               /* (dB) */
        public double[] lowerAbsolutePower;                                               /* (dBm) */
        public double[] upperAbsolutePower;                                               /* (dBm) */
        public double absolutePower;
        public double relativePower;

        public SA(RFmxInstrMX instSession)
        {
            specAn = instSession.GetSpecAnSignalConfiguration();     /* Create a new RFmx Session */
            instrSession = instSession;
            instrSession.ConfigureFrequencyReference("", RFmxInstrMXConstants.PxiClock, 10.0e6);
        }

        public void ConfigureSA(double ReferenceLevel, double CarrierFrequency)
        {
            specAn.SetSelectedPorts("", "if1");
            specAn.ConfigureFrequency("", CarrierFrequency);
            specAn.ConfigureExternalAttenuation("", 0);
            specAn.ConfigureReferenceLevel("", ReferenceLevel);
            specAn.ConfigureDigitalEdgeTrigger("", RFmxInstrMXConstants.PxiTriggerLine1, RFmxSpecAnMXDigitalEdgeTriggerEdge.Rising, 0, true);
            specAn.SelectMeasurements("", RFmxSpecAnMXMeasurementTypes.Acp, true);
            specAn.Acp.Configuration.ConfigureCarrierAndOffsets("", 98.31e6, 1, 100e6);
            specAn.Acp.Configuration.ConfigureMeasurementMethod("", RFmxSpecAnMXAcpMeasurementMethod.Normal);
            specAn.Acp.Configuration.ConfigureNoiseCompensationEnabled("", RFmxSpecAnMXAcpNoiseCompensationEnabled.False);
            specAn.Acp.Configuration.ConfigureSweepTime("", RFmxSpecAnMXAcpSweepTimeAuto.False, 1e-3);
            specAn.Acp.Configuration.SetRbwFilterBandwidth("", 5e3);
        }

        public void InitiateSA(string resultName, bool wait = false)
        {
            if (wait) instrSession.WaitForAcquisitionComplete(10);
            specAn.Initiate("", resultName);
        }

        public void WaitForSAComplete()
        {
            instrSession.WaitForAcquisitionComplete(10);
        }

        public void FetchAcpRecord(string resultName)
        {
            try
            {
                string resultString = RFmxSpecAnMX.BuildResultString(resultName);
                specAn.Acp.Results.FetchOffsetMeasurementArray(resultString, 20, ref lowerRelativePower,
                   ref upperRelativePower, ref lowerAbsolutePower, ref upperAbsolutePower);

                specAn.Acp.Results.FetchCarrierMeasurement(resultString, 20, out absolutePower, out relativePower, out _, out _);
            }

            //Sometimes the analysis thread runs ahead of the acquisition, we can resolve this by waiting for a few miliseconds and trying again 
            catch (RFmxException e) when (e.Message.Contains("-380405"))
            {
                string resultString = RFmxSpecAnMX.BuildResultString(resultName);
                specAn.Acp.Results.FetchOffsetMeasurementArray(resultString, 20, ref lowerRelativePower,
                   ref upperRelativePower, ref lowerAbsolutePower, ref upperAbsolutePower);

                specAn.Acp.Results.FetchCarrierMeasurement(resultString, 20, out absolutePower, out relativePower, out _, out _);
            }
        }
        public void CloseSASession()
        {
            if (specAn != null)
            {
                specAn.Dispose();
                specAn = null;
            }
            if (instrSession != null)
            {
                instrSession.Close();
                instrSession = null;
            }
        }
    }
}
