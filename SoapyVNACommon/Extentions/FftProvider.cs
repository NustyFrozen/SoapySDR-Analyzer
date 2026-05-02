using FFTW.NET;
using MathNet.Numerics;
using MathNet.Numerics.IntegralTransforms;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace SoapyVNACommon.Extentions
{
    public class FftProvider : IDisposable
    {
        // 1. Define the generic delegate signature
        public delegate void FftOperation<T>(T[] input, T[] output);
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        // This is the delegate instance your application will actually call
        public FftOperation<System.Numerics.Complex> ExecuteFft { get; private set; }

        // Keep references to FFTW unmanaged objects so we can dispose them later
        private object _fftwInputArray;
        private object _fftwOutputArray;
        private object _fftwPlan;
        public int size;
        public FftProvider(int fftSize)
        {
            // 2. Check the OS and assign the correct method to the delegate
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Logger.Info("Windows detected: Using FFTW.NET");
                ExecuteFft = InitializeFftw(fftSize);
            }
            else
            {
                Logger.Info("Linux/Unix detected: Using MathNet.Numerics");
                ExecuteFft = InitializeMathNet(fftSize);
            }
            this.size = fftSize;
        }

        // --- LINUX (MathNet) IMPLEMENTATION ---
        private FftOperation<System.Numerics.Complex> InitializeMathNet(int fftSize)
        {
            // MathNet does not require a persistent "Plan" object, 
            // so we just return the execution logic directly.
            return (input, output) =>
            {
                // MathNet does FFT in-place. Copy input to output, then transform output.
                Array.Copy(input, output, input.Length);
                Fourier.Forward(output, FourierOptions.NoScaling);
            };
        }

        // --- WINDOWS (FFTW) IMPLEMENTATION ---
        private FftOperation<System.Numerics.Complex> InitializeFftw(int fftSize)
        {
            // 1. Create the persistent FFTW unmanaged objects once
            var fftwIn = new FftwArrayComplex(fftSize);
            var fftwOut = new FftwArrayComplex(fftSize);
            var plan = FftwPlanC2C.Create(fftwIn, fftwOut, DftDirection.Forwards);

            // Store them so we can dispose them when the provider shuts down
            _fftwInputArray = fftwIn;
            _fftwOutputArray = fftwOut;
            _fftwPlan = plan;

            return (input, output) =>
            {
                // 2. Copy managed C# array to unmanaged FFTW memory
                // Assuming your input is a flat array of I/Q floats, or Complex32
                
                for (int i = 0; i < input.Length; i++)
                {
                    fftwIn[i] = input[i];
                }

                // 3. Execute FFTW Plan
                plan.Execute();

                // 4. Copy unmanaged FFTW memory back to managed C# array
                for (int i = 0; i < output.Length; i++)
                {
                    output[i] = fftwOut[i];
                }
            };
        }

        // 3. Clean up unmanaged FFTW memory when closing the app/stream
        public void Dispose()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Use reflection or cast back to the specific types to dispose
                (_fftwPlan as IDisposable)?.Dispose();
                (_fftwInputArray as IDisposable)?.Dispose();
                (_fftwOutputArray as IDisposable)?.Dispose();
            }
        }
    }
}
