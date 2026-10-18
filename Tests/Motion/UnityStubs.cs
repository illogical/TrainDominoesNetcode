// Only numerical/coroutine support. These are not Unity rendering, projection, or scheduler verification.
using System;
namespace UnityEngine
{
    public struct Vector3
    {
        public float x,y,z;
        public Vector3(float x,float y,float z) { this.x=x;this.y=y;this.z=z; }
        public static Vector3 operator +(Vector3 a,Vector3 b)=>new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);
        public static Vector3 operator *(Vector3 a,float b)=>new Vector3(a.x*b,a.y*b,a.z*b);
        public static Vector3 Lerp(Vector3 a,Vector3 b,float t) {t=Math.Clamp(t,0,1);return new Vector3(a.x+(b.x-a.x)*t,a.y+(b.y-a.y)*t,a.z+(b.z-a.z)*t);}
    }
    public struct Quaternion
    {
        public float x,y,z,w;
        public Quaternion(float x,float y,float z,float w) {this.x=x;this.y=y;this.z=z;this.w=w;}
        public static Quaternion Lerp(Quaternion a,Quaternion b,float t)=>new Quaternion(a.x+(b.x-a.x)*t,a.y+(b.y-a.y)*t,a.z+(b.z-a.z)*t,a.w+(b.w-a.w)*t);
    }
    public class Transform { public Vector3 position,localPosition,localScale; public Quaternion rotation,localRotation; }
    public static class Time {public static float deltaTime;}
    public class WaitForSeconds { public float Seconds;public WaitForSeconds(float seconds){Seconds=seconds;} }
    public class WaitForEndOfFrame {}
    public class AnimationCurve { public float Evaluate(float t)=>t*t*(3-2*t); }
    public static class Random {public static Vector3 insideUnitSphere=>new Vector3();public static float Range(float a,float b)=>a;}
}
public class AnimationDefinition {public float Duration,Delay;public UnityEngine.AnimationCurve Curve;}
