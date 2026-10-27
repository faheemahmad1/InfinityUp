using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;

namespace Poi
{
    public sealed partial class PoiDemoController
    {
        const string CaptureRequestPath = "Docs/play-capture.request";
        const string CaptureResultPath = "Docs/Measurements/playmode.txt";
        const string CaptureDonePath = "Docs/play-capture.done";

        bool captureActive;
        int captureIndex;
        int capturePhase;
        int captureFrames;
        readonly int[] captureSizes = { 1000, 25000, 250000 };
        readonly List<float> frameMs = new List<float>(160);
        readonly StringWriter captureReport = new StringWriter();
        double sumLookup;
        double sumIntegrate;
        double sumRelink;
        double sumPoiGc;
        double sumUpdateGc;
        double sumMarkers;
        double sumExamined;
        double sumMovers;
        double sumProfilerGc;
        int profilerSamples;
        int savedInitialCount;
        float savedHeight;
        float savedAdds;
        float savedRemoves;
        float savedMoving;
        float savedMoveSpeed;
        float savedPlayerSpeed;
        PoiPriority savedPriority;
        int savedMask;
        ProfilerRecorder gcRecorder;
        bool gcRecorderValid;

        void BeginCaptureIfRequested()
        {
            if (!File.Exists(CaptureRequestPath))
            {
                return;
            }

            File.Delete(CaptureRequestPath);
            savedInitialCount = config.initialCount;
            savedHeight = config.cameraHeight;
            savedAdds = config.additionsPerSecond;
            savedRemoves = config.removalsPerSecond;
            savedMoving = config.movingPercent;
            savedMoveSpeed = config.moveSpeed;
            savedPlayerSpeed = config.playerSpeed;
            savedPriority = config.minimumPriority;
            savedMask = config.typeMask;

            config.cameraHeight = 400f;
            config.additionsPerSecond = 20f;
            config.removalsPerSecond = 20f;
            config.movingPercent = 5f;
            config.moveSpeed = 40f;
            config.playerSpeed = 120f;
            config.minimumPriority = PoiPriority.Low;
            config.typeMask = PoiEnums.AllTypesMask;
            captureIndex = 0;
            capturePhase = 0;
            captureFrames = 0;
            Simulator.SetTargetCount(captureSizes[0]);
            gcRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            gcRecorderValid = gcRecorder.Valid;
            if (!gcRecorderValid)
            {
                gcRecorder.Dispose();
                gcRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc");
                gcRecorderValid = gcRecorder.Valid;
            }

            captureReport.WriteLine("Mode: editor play mode");
            captureReport.WriteLine("Unity: " + Application.unityVersion);
            captureReport.WriteLine("Hardware: " + SystemInfo.processorType + " / " + SystemInfo.systemMemorySize + " MB / " + SystemInfo.graphicsDeviceName);
            captureReport.WriteLine("Camera height: 400. Pitch: 90. Moving percent: 5. Adds/sec: 20. Removes/sec: 20. Player speed: 120.");
            captureReport.WriteLine("Sample: 120 frames after 90 warmup frames, once the live count has reached the target.");
            captureReport.WriteLine("Frame time is Time.unscaledDeltaTime. Lookup, simulation, and grid times are the POI stopwatches.");
            captureReport.WriteLine("POI GC and Update GC are GC.GetAllocatedBytesForCurrentThread inside PoiDemoController.Update. Update GC includes the HUD text rebuild.");
            captureReport.WriteLine("Profiler GC marker valid: " + gcRecorderValid + " name: " + (gcRecorderValid ? gcRecorder.ToString() : "none"));
            captureReport.WriteLine("columns: pois, alive, movers, avgFrameMs, p95FrameMs, lookupMs, simMs, gridMs, poiGcBytes, updateGcBytes, profilerGcBytes, markers, examined");
            captureActive = true;
            Directory.CreateDirectory("Docs/Measurements");
            Directory.CreateDirectory("Docs/Profiles");
        }

        void CaptureAfterFrame()
        {
            if (!captureActive || Service == null)
            {
                return;
            }

            try
            {
                config.cameraHeight = 400f;
                if (capturePhase == 0)
                {
                    if (Service.Store.AliveCount == captureSizes[captureIndex] && Simulator.MoverCount > 0)
                    {
                        capturePhase = 1;
                        captureFrames = 0;
                    }

                    return;
                }

                if (capturePhase == 1)
                {
                    captureFrames++;
                    if (captureFrames >= 90 && (MarkersReady || captureFrames >= 600))
                    {
                        if (captureSizes[captureIndex] == 250000)
                        {
                            Profiler.logFile = Path.GetFullPath("Docs/Profiles/poi-250k");
                            Profiler.enableBinaryLog = true;
                            Profiler.enabled = true;
                        }

                        BeginSample();
                    }

                    return;
                }

                frameMs.Add(Time.unscaledDeltaTime * 1000f);
                sumLookup += LastLookupMs;
                sumIntegrate += Simulator.LastIntegrateMs;
                sumRelink += Simulator.LastRelinkMs;
                sumPoiGc += LastPoiAllocatedBytes;
                sumUpdateGc += LastTotalFrameAllocatedBytes;
                sumMarkers += ActiveMarkers;
                sumExamined += LastExamined;
                sumMovers += Simulator.MoverCount;
                if (gcRecorderValid)
                {
                    sumProfilerGc += gcRecorder.LastValue;
                    profilerSamples++;
                }

                captureFrames++;
                if (captureFrames < 120)
                {
                    return;
                }

                WriteCaptureRow();
                if (captureSizes[captureIndex] == 250000)
                {
                    Profiler.enableBinaryLog = false;
                    Profiler.logFile = string.Empty;
                }

                captureIndex++;
                if (captureIndex >= captureSizes.Length)
                {
                    FinishCapture();
                    return;
                }

                Simulator.SetTargetCount(captureSizes[captureIndex]);
                capturePhase = 0;
                captureFrames = 0;
            }
            catch (Exception exception)
            {
                captureReport.WriteLine("FAILED during size " + captureSizes[Mathf.Clamp(captureIndex, 0, captureSizes.Length - 1)]);
                captureReport.WriteLine(exception.ToString());
                FinishCapture();
            }
        }

        void BeginSample()
        {
            capturePhase = 2;
            captureFrames = 0;
            frameMs.Clear();
            sumLookup = 0d;
            sumIntegrate = 0d;
            sumRelink = 0d;
            sumPoiGc = 0d;
            sumUpdateGc = 0d;
            sumMarkers = 0d;
            sumExamined = 0d;
            sumMovers = 0d;
            sumProfilerGc = 0d;
            profilerSamples = 0;
        }

        void WriteCaptureRow()
        {
            int n = frameMs.Count;
            double avg = 0d;
            for (int i = 0; i < n; i++)
            {
                avg += frameMs[i];
            }

            avg /= n;
            var sorted = frameMs.ToArray();
            Array.Sort(sorted);
            int p95Index = (int)Math.Ceiling(0.95d * n) - 1;
            if (p95Index < 0)
            {
                p95Index = 0;
            }

            string profilerCell = profilerSamples > 0
                ? ((long)(sumProfilerGc / profilerSamples)).ToString(CultureInfo.InvariantCulture)
                : "unavailable";
            captureReport.WriteLine(string.Format(
                CultureInfo.InvariantCulture,
                "{0},{1},{2:0.0},{3:0.000},{4:0.000},{5:0.000},{6:0.000},{7:0.000},{8:0},{9:0},{10},{11:0.0},{12:0.0}",
                captureSizes[captureIndex],
                Service.Store.AliveCount,
                sumMovers / n,
                avg,
                sorted[p95Index],
                sumLookup / n,
                sumIntegrate / n,
                sumRelink / n,
                sumPoiGc / n,
                sumUpdateGc / n,
                profilerCell,
                sumMarkers / n,
                sumExamined / n));
        }

        void FinishCapture()
        {
            captureActive = false;
            config.initialCount = savedInitialCount;
            config.cameraHeight = savedHeight;
            config.additionsPerSecond = savedAdds;
            config.removalsPerSecond = savedRemoves;
            config.movingPercent = savedMoving;
            config.moveSpeed = savedMoveSpeed;
            config.playerSpeed = savedPlayerSpeed;
            config.minimumPriority = savedPriority;
            config.typeMask = savedMask;
            Simulator.SetTargetCount(savedInitialCount);
            if (gcRecorder.Valid)
            {
                gcRecorder.Dispose();
            }

            Directory.CreateDirectory("Docs/Measurements");
            File.WriteAllText(CaptureResultPath, captureReport.ToString());
            File.WriteAllText(CaptureDonePath, "done");
        }
    }
}
