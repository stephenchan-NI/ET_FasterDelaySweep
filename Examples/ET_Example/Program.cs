﻿using NationalInstruments.ModularInstruments.NIRfsg;
using NationalInstruments.ReferenceDesignLibraries;
using System;
using System.Collections.Generic;
using static NationalInstruments.ReferenceDesignLibraries.Methods.EnvelopeTracking;
using static NationalInstruments.ReferenceDesignLibraries.SG;
using SA;
using NationalInstruments.RFmx.InstrMX;

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
            string waveformPath = @"C:\Users\LocalAdmin\Documents\GitHub\ET_DelaySweep\ET_FasterDelaySweep\Examples\ET_Example\wfm\5ms_256QAM_100MHz_30kHzSCS.tdms";
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

            //Initiate RFmx acquisition with unique result name
            string resultName = "acpResult";
            rfmxSession.InitiateSA(resultName + "0", false);

            double sampleDelayPeriod = 1 / rfVsg.Arb.IQRate;
            Console.WriteLine("Generating " + numberOfSteps.ToString() + " delay steps");
            Console.WriteLine("RFSG Arb IQ rate = " + (rfVsg.Arb.IQRate / 1e6).ToString() + " MHz");
            Console.WriteLine("Delay step size = " + (sampleDelayPeriod * 1e9).ToString() + " nanoseconds");
            Console.WriteLine("Initiating Generation");

            // Start envelope tracking
            SynchronizationConfiguration syncConfig = SynchronizationConfiguration.GetDefault();
            //Start fetching records in a loop
            //Subtract 1 from "numberOfSteps" as we already initiated single measurement 
            for (long i = 1; i < numberOfSteps; i++)
            {
                //This loop will initiate each measurement without waiting for the results to be ready for fetching
                if (i == 1)
                {
                    InitiateSynchronousGeneration(rfVsg, envVsg, syncConfig);
                }
                rfmxSession.InitiateSA(resultName + i.ToString(), true);
            }

            List<double> lowerRelativePower = new List<double>();
            List<double> upperRelativePower = new List<double>();
            List<double> channelPower = new List<double>();

            for (long i = 0; i < numberOfSteps; i++)
            {
                rfmxSession.FetchAcpRecord(resultName + i.ToString());
                lowerRelativePower.Add(rfmxSession.lowerRelativePower[0]);
                upperRelativePower.Add(rfmxSession.upperRelativePower[0]);
                channelPower.Add(rfmxSession.absolutePower);
            }



            // Wait until user presses a button to stop
            Console.WriteLine("Press any key to abort...");
            Console.ReadKey();

            AbortGeneration(envVsg);
            AbortGeneration(rfVsg);


            // Close instruments
            rfVsg.Close();
            envVsg.Close();
            rfmxSession.CloseSASession();
        }
    }
}
