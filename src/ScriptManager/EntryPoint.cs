﻿namespace ScTools
{
    using System;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using System.Threading;

    using Avalonia;
    using Avalonia.Controls;
    using Avalonia.Controls.ApplicationLifetimes;
    using Avalonia.Logging.Serilog;
    using Avalonia.Threading;

    using ScTools.UI;
    using ScTools.UI.ViewModels;
    using ScTools.ViewModels;

    internal static class EntryPoint
    {
        static IntPtr uiThreadHandle;

        [DllImport("kernel32.dll")]
        static unsafe extern IntPtr CreateThread(IntPtr lpThreadAttributes, ulong dwStackSize, delegate* unmanaged[Stdcall]<IntPtr, int> lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, IntPtr lpThreadId);

        [UnmanagedCallersOnly(EntryPoint = nameof(DllMain), CallConvs = new[] { typeof(CallConvStdcall) })]
        public static bool DllMain(IntPtr hDllHandle, uint nReason, IntPtr Reserved)
        {
            const int DLL_PROCESS_ATTACH = 1;

            if (nReason == DLL_PROCESS_ATTACH)
            {
                Console.WriteLine("DLL_PROCESS_ATTACH");

                // CreateThread instead of System.Threading.Thread because it gets stuck in Thread.Start
                unsafe { uiThreadHandle = CreateThread(IntPtr.Zero, 0, &Init, IntPtr.Zero, 0, IntPtr.Zero); }

                Console.WriteLine($"Thread created (handle: {uiThreadHandle.ToString("X")})");
            }

            return true;
        }

        [UnmanagedCallersOnly(EntryPoint = nameof(GetUIThreadHandle))]
        public static IntPtr GetUIThreadHandle() => uiThreadHandle;

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
        private static int Init(IntPtr parameter)
        {
            Console.WriteLine($"Init (in-game: {Util.IsInGame})");
            if (Util.IsInGame)
            {
                while (Process.GetCurrentProcess().MainWindowHandle == IntPtr.Zero)
                {
                    Console.WriteLine("Waiting for game window");
                    Thread.Sleep(5000);
                }
                Thread.Sleep(5000);
            }

            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(Array.Empty<string>());

            Console.WriteLine("Init End");
            return 0;
        }

        private static AppBuilder BuildAvaloniaApp() =>
            Util.IsInGame ? 
                App.BuildApp<ProgramsViewModelImpl, ThreadsViewModel>() :
                App.BuildApp<DummyProgramsViewModel, ThreadsViewModel>();
    }
}
