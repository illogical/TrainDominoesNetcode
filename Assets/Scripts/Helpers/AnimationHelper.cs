using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class AnimationHelper
{
    // Keep a smooth acceleration/deceleration fallback for definitions without an authored curve.
    private static float Ease(AnimationDefinition definition, float progress) => definition != null && definition.Curve != null ? definition.Curve.Evaluate(progress) : progress * progress * (3f - 2f * progress);

    /// <summary>
    /// Reproducible, modest variation for the initial deal's parallel columns. Never extends the authored
    /// batch deadline (duration + stagger * (count - 1)), and does not consume the gameplay RNG.
    /// Timing alone is not collision avoidance: callers must keep those columns spatially separated.
    /// </summary>
    public static void DealTiming(int index, int count, int seed, float duration, float stagger, out float seconds, out float delay)
    {
        uint hash;
        unchecked
        {
            hash = (uint)seed ^ ((uint)index + 1u) * 0x9e3779b9u;
            hash ^= hash >> 16; hash *= 0x7feb352du;
            hash ^= hash >> 15; hash *= 0x846ca68bu;
            hash ^= hash >> 16;
        }
        float departure = (hash & 0xffffu) / 65535f;
        float arrival = (hash >> 16) / 65535f;
        seconds = System.Math.Max(0, duration) * (0.92f + 0.08f * arrival);
        float step = System.Math.Max(0, stagger);
        delay = System.Math.Max(0, System.Math.Min(step * System.Math.Max(0, count - 1), step * (index + (departure - 0.5f) * 0.4f)));
    }

    public static IEnumerator MoveOverSeconds(Transform transform, Vector3 endPos, AnimationDefinition animationDefinition)
    {
        yield return new WaitForSeconds((animationDefinition != null ? animationDefinition.Delay : 0));

        float elapsedTime = 0;
        var startPos = transform.position;
        while (elapsedTime < (animationDefinition != null ? animationDefinition.Duration : 0))
        {
            transform.position = Vector3.Lerp(startPos, endPos, Ease(animationDefinition, elapsedTime / animationDefinition.Duration));
            elapsedTime += Time.deltaTime;
            yield return new WaitForEndOfFrame();
        }
        transform.position = endPos;
    }

    public static IEnumerator MoveOverSeconds(Transform transform, Vector3 endPos, float seconds, float delay, AnimationDefinition curve)
    {
        yield return new WaitForSeconds(delay);

        float elapsedTime = 0;
        var startPos = transform.position;
        while (elapsedTime < seconds)
        {
            transform.position = Vector3.Lerp(startPos, endPos, Ease(curve, elapsedTime / seconds));
            elapsedTime += Time.deltaTime;
            yield return new WaitForEndOfFrame();
        }
        transform.position = endPos;
    }

    /// <summary>
    /// Moves in the parent's local space (VIEW-001). A tile under a board row keeps travelling with that row while
    /// the player pans or zooms, so it lands on its target instead of where the target was when the move began.
    /// Rotation and scale are optional and share the same timing.
    /// </summary>
    public static IEnumerator MoveLocalOverSeconds(Transform transform, Vector3 endLocal, Quaternion? endRotation, Vector3? endScale, float seconds, float delay, AnimationDefinition curve)
    {
        if (delay > 0) yield return new WaitForSeconds(delay);
        float elapsedTime = 0;
        var startPos = transform.localPosition;
        var startRotation = transform.localRotation;
        var startScale = transform.localScale;
        while (elapsedTime < seconds)
        {
            float t = Ease(curve, elapsedTime / seconds);
            transform.localPosition = Vector3.Lerp(startPos, endLocal, t);
            if (endRotation.HasValue) transform.localRotation = Quaternion.Lerp(startRotation, endRotation.Value, t);
            if (endScale.HasValue) transform.localScale = Vector3.Lerp(startScale, endScale.Value, t);
            elapsedTime += Time.deltaTime;
            yield return new WaitForEndOfFrame();
        }
        transform.localPosition = endLocal;
        if (endRotation.HasValue) transform.localRotation = endRotation.Value;
        if (endScale.HasValue) transform.localScale = endScale.Value;
    }

    public static IEnumerator MoveLocalOverSeconds(Transform transform, Vector3 endLocal, Vector3? endScale, AnimationDefinition definition)
    {
        return MoveLocalOverSeconds(transform, endLocal, null, endScale, definition != null ? definition.Duration : 0, definition != null ? definition.Delay : 0, definition);
    }

    public static IEnumerator RotateLocalOverSeconds(Transform transform, Quaternion endRotation, float seconds, float delay, AnimationDefinition curve)
    {
        if (delay > 0) yield return new WaitForSeconds(delay);
        float elapsedTime = 0;
        var startRotation = transform.localRotation;
        while (elapsedTime < seconds)
        {
            transform.localRotation = Quaternion.Lerp(startRotation, endRotation, Ease(curve, elapsedTime / seconds));
            elapsedTime += Time.deltaTime;
            yield return new WaitForEndOfFrame();
        }
        transform.localRotation = endRotation;
    }

    public static IEnumerator ScaleOverSeconds(Transform transform, float scale, AnimationDefinition animationDefinition)
    {
        yield return new WaitForSeconds((animationDefinition != null ? animationDefinition.Delay : 0));

        float elapsedTime = 0;
        var startSize = transform.localScale;
        var endSize = new Vector3(transform.localScale.x * scale, transform.localScale.y * scale, transform.localScale.z * scale);
        while (elapsedTime < (animationDefinition != null ? animationDefinition.Duration : 0))
        {
            transform.localScale = Vector3.Lerp(startSize, endSize, Ease(animationDefinition, elapsedTime / animationDefinition.Duration));
            elapsedTime += Time.deltaTime;
            yield return new WaitForEndOfFrame();
        }
        transform.localScale = endSize;
    }

    public static IEnumerator RotateOverSeconds(Transform transform, Quaternion rotationAmount, float seconds, float delay, AnimationDefinition curve)
    {
        yield return new WaitForSeconds(delay);

        float elapsedTime = 0;
        var startRotation = transform.rotation;
        while (elapsedTime < seconds)
        {
            transform.rotation = Quaternion.Lerp(startRotation, rotationAmount, Ease(curve, elapsedTime / seconds));
            elapsedTime += Time.deltaTime;
            yield return new WaitForEndOfFrame();
        }
        transform.rotation = rotationAmount; // TODO: is this necessary?
    }

    public static IEnumerator MoveOverSecondsWithDepth(Transform transform, Vector3 endPos, float depth, AnimationDefinition animationDefinition)
    {
        // TODO: allow depth speed to be passed in
        float depthAnimationSeconds = 0.15f;

        float startDepth = transform.position.z;
        Vector3 startPosWithDepth = new Vector3(transform.position.x, transform.position.y, startDepth - depth);
        Vector3 endPosWithDepth = new Vector3(endPos.x, endPos.y, startDepth - depth);

        yield return MoveOverSeconds(transform, startPosWithDepth, animationDefinition);
        yield return MoveOverSeconds(transform, endPosWithDepth, animationDefinition);
        yield return MoveOverSeconds(transform, endPos, animationDefinition);
    }

    public static IEnumerator MoveOverSecondsWithDepthReturn(Transform transform, Vector3 endPos, float seconds, float delay, float depthOffset, AnimationDefinition animationDefinition)
    {
        Vector3 endPosWithDepth = new Vector3(endPos.x, endPos.y, endPos.z + depthOffset);
        yield return MoveOverSeconds(transform, endPosWithDepth, seconds, delay, animationDefinition);
        yield return MoveOverSeconds(transform, endPos, 0.15f, 0, animationDefinition);
    }

    public static IEnumerator Jiggle(Transform transform, AnimationCurve curve)
    {
        Vector3 originPosition = new Vector3(transform.position.x, transform.position.y, transform.position.z);
        Quaternion originRotation = new Quaternion(transform.rotation.x, transform.rotation.y, transform.rotation.z, transform.rotation.w);
        float shake_decay = 0.002f;
        float shake_intensity = 0.4f;
        float reduceShakeDistance = 0.3f;
        float temp_shake_intensity = shake_intensity;

        while (temp_shake_intensity > 0)
        {
            transform.position = originPosition + new Vector3(UnityEngine.Random.insideUnitSphere.x * temp_shake_intensity * reduceShakeDistance, 0, 0);
            transform.rotation = new Quaternion(
                originRotation.x + UnityEngine.Random.Range(-temp_shake_intensity, temp_shake_intensity) * reduceShakeDistance,
                originRotation.y + UnityEngine.Random.Range(-temp_shake_intensity, temp_shake_intensity) * reduceShakeDistance,
                originRotation.z,
                originRotation.w);
            temp_shake_intensity -= shake_decay;
            yield return new WaitForEndOfFrame();
        }

        transform.position = originPosition;
        transform.rotation = originRotation;
    }

    public static IEnumerator OneAfterTheOther(params IEnumerator[] routines)
    {
        foreach (var item in routines)
        {
            while (item.MoveNext()) yield return item.Current;
        }

        yield break;
    }
}
