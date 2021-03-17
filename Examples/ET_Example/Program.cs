using NationalInstruments.ModularInstruments.NIRfsg;
using NationalInstruments.ModularInstruments.NIRfsa;
using NationalInstruments.ReferenceDesignLibraries;
using System;
using static NationalInstruments.ReferenceDesignLibraries.Methods.EnvelopeTracking;
using static NationalInstruments.ReferenceDesignLibraries.SG;
using static SA.SA;

namespace EnvelopeTrackingExample
{
    class Program
    {
        internal enum EnvelopeMode { Detrough, LUT };

        /// <summary>
        /// This example illustrates how to use RFSG drivers and envelope tracking APIs to configure envelope tracking. 
        /// </summary>
        static void Main(string[] args)
        {
            #region Example Settings
            // Select mode for use in the example
            EnvelopeMode mode = EnvelopeMode.Detrough;
            string waveformPath = @"C:\Users\Public\Documents\National Instruments\RFIC Test Software\Waveforms\LTE_FDD_DL_1x20MHz_TM11_OS4.tdms";
            #endregion

            #region Configure RF Generator
            // Initialize instrument sessions
            string rfVsgResourceName = "BCN_01";
            NIRfsg rfVsg = new NIRfsg(rfVsgResourceName, true, false);

            // Load up waveform
            Waveform[] rfWfms = LoadWaveformFromTDMS(waveformPath);

            // Configure RF generator
            InstrumentConfiguration rfInstrConfig = InstrumentConfiguration.GetDefault();
            ConfigureInstrument(rfVsg, rfInstrConfig);
            foreach (Waveform wfm in rfWfms)
            {
                DownloadWaveform(rfVsg, wfm);
            }
            int numberOfSteps = 200;
            ConfigureContinuousDelayGeneration(rfVsg, rfWfms, numberOfSteps, "PXI_Trig1");
            #endregion

            #region Configure Tracker Generator
            string envVsgResourceName = "5820_03";
            NIRfsg envVsg = new NIRfsg(envVsgResourceName, true, false);

            // Configure envelope generator
            EnvelopeGeneratorConfiguration envInstrConfig = EnvelopeGeneratorConfiguration.GetDefault();
            TrackerConfiguration trackerConfig = TrackerConfiguration.GetDefault();
            ConfigureEnvelopeGenerator(envVsg, envInstrConfig, trackerConfig);

            Waveform envWfm = new Waveform();
            Waveform rfWfm = rfWfms[0];
            switch (mode)
            {
                case EnvelopeMode.Detrough:
                    // Create envelope waveform
                    DetroughConfiguration detroughConfig = DetroughConfiguration.GetDefault();
                    detroughConfig.MinimumVoltage_V = 1.5;
                    detroughConfig.MaximumVoltage_V = 3.5;
                    detroughConfig.Exponent = 1.2;
                    detroughConfig.Type = DetroughType.Exponential;
                    envWfm = CreateDetroughEnvelopeWaveform(rfWfm, detroughConfig);
                    break;
                case EnvelopeMode.LUT:
                    LookUpTableConfiguration lutConfig = new LookUpTableConfiguration
                    {
                        DutAverageInputPower_dBm = rfInstrConfig.DutAverageInputPower_dBm
                    };
                    // Todo - initialize lookup table
                    envWfm = CreateLookUpTableEnvelopeWaveform(rfWfm, lutConfig);
                    break;
            }

            ScaleAndDownloadEnvelopeWaveform(envVsg, envWfm, trackerConfig);
            ConfigureContinuousGeneration(envVsg, envWfm, numberOfSteps, "PXI_Trig0");
            #endregion

            #region Configure RF Analyzer
            string rfsaResourceName = "BCN_01";
            NIRfsa rfsaVsa = new NIRfsa(rfsaResourceName, false, true);

            double referenceLevel = 0;
            double carrierFrequency = 5e9;
            long numberOfSamples = 1000;
            long numberOfRecords = Convert.ToInt64(numberOfSteps);
            double iqRate = 400e6;

            ConfigureSA(rfsaVsa, referenceLevel, carrierFrequency, numberOfSamples, numberOfRecords, iqRate);
            InitiateSA(rfsaVsa);
            #endregion

            double sampleDelayPeriod = 1 / rfVsg.Arb.IQRate;
            Console.WriteLine("Generating " + numberOfSteps.ToString() + " delay steps");
            Console.WriteLine("RFSG Arb IQ rate = " + (rfVsg.Arb.IQRate / 1e6).ToString() + " MHz");
            Console.WriteLine("Delay step size = " + (sampleDelayPeriod * 1e9).ToString() + " nanoseconds");
            Console.WriteLine("Initiating Generation");

            // Start envelope tracking
            SynchronizationConfiguration syncConfig = SynchronizationConfiguration.GetDefault();
            InitiateSynchronousGeneration(rfVsg, envVsg, syncConfig);

            //Start fetching records in a loop
            for (long i = 0; i < numberOfRecords; i++)
            {
                //This loop will fetch IQ records as they become available
                var outputWfm = FetchIQRecord(rfsaVsa, i, numberOfSamples);

                //As each record is fetched, an asynchronous measurement can be performed on the IQ data. 
                //For example, outputWfm can be passed to a preconfigured RFmx AOM thread that begins processing the measurement while the next record is fetched
            }

            // Wait until user presses a button to stop

            Console.WriteLine("Press any key to abort...");
            Console.ReadKey();

            AbortGeneration(envVsg);
            AbortGeneration(rfVsg);


            // Close instruments
            rfVsg.Close();
            envVsg.Close();
            CloseSASession(rfsaVsa);
        }
    }
}
