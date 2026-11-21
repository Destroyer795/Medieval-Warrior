using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

// Opt-in CPU timings for load/spawn regression captures. No sampling or logging
// runs during ordinary play; scopes are allocation-free when disabled.
public static class BattleLoadMetrics
{
    [Serializable] public sealed class Sample {public string stage;public double milliseconds;public int frame;}
    public static bool Enabled;
    public static readonly List<Sample> Samples=new List<Sample>();
    public readonly struct Scope : IDisposable
    {
        readonly string stage;
        readonly long start;
        public Scope(string name){stage=Enabled?name:null;start=stage!=null?Stopwatch.GetTimestamp():0;}
        public void Dispose()
        {
            if(stage!=null)Samples.Add(new Sample{stage=stage,milliseconds=(Stopwatch.GetTimestamp()-start)*1000d/Stopwatch.Frequency,frame=Time.frameCount});
        }
    }
    public static Scope Measure(string stage)=>new Scope(stage);
}
