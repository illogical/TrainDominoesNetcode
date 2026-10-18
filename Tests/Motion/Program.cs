using System;
using System.Collections;
using UnityEngine;

static class Program
{
    static int checks;
    static void Check(bool condition,string message) {if(!condition)throw new Exception(message);checks++;}
    static void Main()
    {
        // Every seed/count gets an ordered, bounded schedule; gameplay RNG is absent from the linked helper.
        for(int seed=0;seed<100;seed++) for(int count=1;count<=91;count++)
        {
            float previous=-1;
            for(int i=0;i<count;i++)
            {
                AnimationHelper.DealTiming(i,count,seed,.5f,.025f,out float duration,out float delay);
                AnimationHelper.DealTiming(i,count,seed,.5f,.025f,out float duration2,out float delay2);
                Check(duration==duration2&&delay==delay2,"deterministic seed");
                Check(duration>=.46f&&duration<=.5f&&delay>=previous,"bounded duration and ordered departures");
                Check(delay+duration<=.5f+.025f*(count-1)+1e-6f,"authored batch deadline");
                previous=delay;
            }
        }
        AnimationHelper.DealTiming(0,1,1,-1,-1,out float zeroDuration,out float zeroDelay);
        Check(zeroDuration==0&&zeroDelay==0,"nonpositive timing snaps immediately");
        SampleDeal(16); SampleDeal(91);
        var target=new Transform(); Time.deltaTime=1f/240;
        var move=AnimationHelper.MoveLocalOverSeconds(target,new Vector3(1,0,0),null,null,1,0,null);
        float previousX=0,firstStep=0;
        for(int i=0;move.MoveNext();i++)
        {
            Check(target.localPosition.x>=previousX&&target.localPosition.x<=1,"smooth fallback monotone without overshoot");
            if(i==1)firstStep=target.localPosition.x;
            previousX=target.localPosition.x;
        }
        Check(firstStep<.0001f&&target.localPosition.x==1,"smooth departure and exact endpoint");
        Console.WriteLine($"PASS {checks} timing/motion checks. Linked production AnimationHelper; synthetic orthographic footprint sampling only. No Unity/visual acceptance.");
    }

    static void SampleDeal(int count)
    {
        const float width=.0125f,length=.025f,dt=1f/240;
        Time.deltaTime=dt;
        var tiles=new Transform[count]; var routines=new IEnumerator[count]; var wake=new float[count]; var done=new bool[count];
        for(int i=0;i<count;i++)
        {
            tiles[i]=new Transform{localPosition=new Vector3(i*width,-length*1.5f,0)};
            AnimationHelper.DealTiming(i,count,1729,.5f,.025f,out float duration,out float delay);
            routines[i]=AnimationHelper.MoveLocalOverSeconds(tiles[i],new Vector3(i*width,0,0),null,null,duration,delay,null);
        }
        // Synthetic orthographic panning/zooming, with fixed unrotated hand tiles. Positive-area
        // rectangle intersections count beyond 1e-6 world units of float roundoff; edge contact does not. This intentionally claims nothing about
        // rotated placement/draw/undo trajectories, resting board rows or real camera projection.
        for(float t=0;t<3;t+=dt)
        {
            for(int i=0;i<count;i++) if(!done[i]&&t>=wake[i])
            {
                done[i]=!routines[i].MoveNext();
                if(!done[i]&&routines[i].Current is WaitForSeconds wait) wake[i]=t+wait.Seconds;
            }
            float zoom=.5f+(float)Math.Abs(Math.Sin(t)),pan=(float)Math.Sin(t)*.05f;
            for(int i=0;i<count-1;i++)
            {
                var a=tiles[i].localPosition;var b=tiles[i+1].localPosition;
                float overlapX=Math.Min((a.x+width/2)*zoom+pan,(b.x+width/2)*zoom+pan)-Math.Max((a.x-width/2)*zoom+pan,(b.x-width/2)*zoom+pan);
                float overlapY=Math.Min((a.y+length/2)*zoom,(b.y+length/2)*zoom)-Math.Max((a.y-length/2)*zoom,(b.y-length/2)*zoom);
                Check(overlapX<=1e-6f||overlapY<=1e-6f,"positive-area adjacent deal-column overlap");
            }
        }
        for(int i=0;i<count;i++)Check(done[i]&&tiles[i].localPosition.y==0,"deal endpoint before deadline");
    }
}
