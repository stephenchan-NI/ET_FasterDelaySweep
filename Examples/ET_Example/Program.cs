using NationalInstruments.ModularInstruments.NIRfsg;
using NationalInstruments.ReferenceDesignLibraries;
using System;
using System.Collections.Generic;
using static NationalInstruments.ReferenceDesignLibraries.Methods.EnvelopeTracking;
using static NationalInstruments.ReferenceDesignLibraries.SG;
using SA;
using NationalInstruments.ModularInstruments.SystemServices.TimingServices;
using NationalInstruments.RFmx.InstrMX;
using System.Threading;
using System.Diagnostics;

namespace EnvelopeTrackingExample
{
    class Program
    {

        internal static List<double> lowerRelativePower = new List<double>();
        internal static List<double> upperRelativePower = new List<double>();
        internal static List<double> channelPower = new List<double>();

        internal enum EnvelopeMode { Detrough, LUT };

        /// <summary>
        /// This example illustrates how to use RFSG drivers and envelope tracking APIs to configure envelope tracking. 
        /// </summary>
        static void Main(string[] args)
        {
            #region Example Settings
            // Select mode for use in the example
            EnvelopeMode mode = EnvelopeMode.Detrough;
            string waveformPath = @"C:\Users\LocalAdmin\Documents\GitHub\ET_DelaySweep\ET_FasterDelaySweep\Examples\ET_Example\wfm\1ms_NR_FR1_DL_FDD_SISO_BW-100MHz_CC-1_SCS-30kHz_Mod-256QAM-OFDM-NoSSB.tdms";
            #endregion

            #region Configure RF Analyzer
            string rfsaResourceName = "BCN_02";
            RFmxInstrMX rfmxVsa = new RFmxInstrMX(rfsaResourceName, "");

            double referenceLevel = 10;
            double carrierFrequency = 5e9;

            SA.SA rfmxSession = new SA.SA(rfmxVsa);
            rfmxSession.ConfigureSA(referenceLevel, carrierFrequency);

            #endregion
            #region Configure RF Generator
            // Initialize instrument sessions
            string rfVsgResourceName = "BCN_02";
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
            ConfigureContinuousGeneration(rfVsg, rfWfms[0], "PXI_Trig1");
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
            ConfigureContinuousGeneration(envVsg, envWfm, "PXI_Trig0");
            #endregion

            //Initiate RFmx acquisition with unique result name
            //We want to initiate this first so that when the fetch thread is started, there is already a result populated.
            string resultName = "acpResult";
            rfmxSession.InitiateSA(resultName + 0.ToString());

            //Initialize Timer
            Stopwatch stopWatch = new Stopwatch();
            Stopwatch acqTimer = new Stopwatch();

            // Start envelope tracking
            SynchronizationConfiguration syncConfig = SynchronizationConfiguration.GetDefault();
            TClock etSessions = new TClock();
            InitiateSynchronousGeneration(rfVsg, envVsg, syncConfig, out etSessions);

            //initialize delay sweep parameters
            double numberOfSteps = 1000;
            double stepSize = 1e-9;

            //Start the thread to fetch ACP asynchronously. 
            //Thread asyncFetch = new Thread(() => AsynchronousFetch(rfmxSession, numberOfSteps, resultName));
            //asyncFetch.Start();

            //Start timer
            stopWatch.Start();

            //Start iterating over delay values
            for (double i = 1; i < numberOfSteps; i++)
            {
                rfmxSession.InitiateSA(resultName + i.ToString(), true);
                //This loop will initiate each measurement without waiting for the results to be ready for fetching
                AdjustSynchronousGeneration(etSessions, (stepSize*i), syncConfig, rfVsg);

                //Code below demonstrates sequential fetching, where each measurement must be fetched before proceeding to the next iteration 
                /*
                rfmxSession.FetchAcpRecord(resultName + i.ToString());
                lowerRelativePower.Add(rfmxSession.lowerRelativePower[0]);
                upperRelativePower.Add(rfmxSession.upperRelativePower[0]);
                channelPower.Add(rfmxSession.absolutePower);
                */
            }
            //Code below demonstrates fetching after all hardware acquisitions are completed.
            //Note that the measurement analysis still occurs in parallel with the hardware acquisitions, but only the fetching of the results is done sequentially below
            AsynchronousFetch(rfmxSession, numberOfSteps, resultName);
            
            //Finish fetch thread
            //asyncFetch.Join();
            //asyncFetch.Abort();
            stopWatch.Stop();
            // Format and display the TimeSpan value.
            TimeSpan ts = stopWatch.Elapsed;
            string elapsedTime = ts.TotalMilliseconds.ToString();

            // Wait until user presses a button to stop
            Console.WriteLine("Run time = " + elapsedTime);
            Console.WriteLine("Time per acquisition and fetch = " + Convert.ToDouble(elapsedTime) / numberOfSteps + "ms per step");
            Console.WriteLine("Output Channel power = " + channelPower[0].ToString() + "dB");
            Console.WriteLine("Press any key to abort...");
            Console.ReadKey();

            AbortGeneration(envVsg);
            AbortGeneration(rfVsg);


            // Close instruments
            rfVsg.Close();
            envVsg.Close();
            rfmxSession.CloseSASession();
        }

        public static void AsynchronousFetch(SA.SA rfmxSession, double numberOfSteps, string resultName)
        {
            for (long i = 0; i < numberOfSteps; i++)
            {
                rfmxSession.FetchAcpRecord(resultName + i.ToString());
                lowerRelativePower.Add(rfmxSession.lowerRelativePower[0]);
                upperRelativePower.Add(rfmxSession.upperRelativePower[0]);
                channelPower.Add(rfmxSession.absolutePower);
            }
        }
    }
}
