using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NationalInstruments.ModularInstruments.NIRfsa;
using NationalInstruments;

namespace SA
{
    public class SA
    {
        public static void ConfigureSA(NIRfsa rfsaSession, double ReferenceLevel, double CarrierFrequency, long NumberOfSamples, long NumberOfRecords, double IQRate)
        {
            // Configure reference clock
            rfsaSession.Configuration.ReferenceClock.Source = RfsaReferenceClockSource.PxiClock;
            rfsaSession.Configuration.ReferenceClock.Rate = 10E6;

            //Configure IQ acquisition 
            rfsaSession.Configuration.AcquisitionType = RfsaAcquisitionType.IQ;
            rfsaSession.Configuration.Vertical.ReferenceLevel = ReferenceLevel;
            rfsaSession.Configuration.IQ.CarrierFrequency = CarrierFrequency;
            rfsaSession.Configuration.IQ.NumberOfSamples = NumberOfSamples;
            rfsaSession.Configuration.IQ.NumberOfSamplesIsFinite = true;
            rfsaSession.Configuration.IQ.NumberOfRecords = NumberOfRecords;
            rfsaSession.Configuration.IQ.NumberOfRecordsIsFinite = true;
            rfsaSession.Configuration.IQ.IQRate = IQRate;

            //Configure Trigger
            rfsaSession.Configuration.Triggers.ReferenceTrigger.Type = RfsaReferenceTriggerType.DigitalEdge;
            rfsaSession.Configuration.Triggers.ReferenceTrigger.DigitalEdge.Edge = RfsaTriggerEdge.Rising;
            rfsaSession.Configuration.Triggers.ReferenceTrigger.DigitalEdge.Source = RfsaDigitalEdgeReferenceTriggerSource.PxiTriggerLine1;
            rfsaSession.Configuration.Triggers.ReferenceTrigger.PreTriggerSamples = 0;
        }

        public static void InitiateSA(NIRfsa rfsaSession)
        {
            rfsaSession.Acquisition.IQ.Initiate();
        }

        public static ComplexDouble[] FetchIQRecord(NIRfsa rfsaSession, long recordNumber, long numberOfSamples)
        {
            PrecisionTimeSpan timeout = new PrecisionTimeSpan(10); //10 Seconds
            RfsaWaveformInfo wfmInfo = new RfsaWaveformInfo();
            ComplexDouble[] outputWfm;
            outputWfm = rfsaSession.Acquisition.IQ.FetchIQSingleRecordComplex<ComplexDouble>(recordNumber, numberOfSamples, timeout, out wfmInfo);
            return outputWfm;
        }
        public static void CloseSASession(NIRfsa rfsaSession)
        {
            if (rfsaSession != null)
            {
                try
                {
                    rfsaSession.Close();
                    rfsaSession = null;
                }
                catch (System.Exception ex)
                {
                    Console.WriteLine("Unable to Close Session, Reset the device.\n" + "Error : " + ex.Message);
                }
            }
        }

    }
}
