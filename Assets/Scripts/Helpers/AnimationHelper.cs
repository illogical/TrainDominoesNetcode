using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class AnimationHelper
{
    public static IEnumerator MoveOverSeconds(Transform transform, Vector3 endPos, float seconds, float delay, AnimationCurve curve)
    {
        yield return new WaitForSeconds(delay);

        float elapsedTime = 0;
        var startPos = transform.position;
        while (elapsedTime < seconds)
        {
            transform.position = Vector3.Lerp(startPos, endPos, curve.Evaluate(elapsedTime / seconds));
            elapsedTime += Time.deltaTime;
            yield return new WaitForEndOfFrame();
        }
        transform.position = endPos;
    }

    public static IEnumerator ScaleOverSeconds(Transform transform, float scale, float seconds, float delay, AnimationCurve curve)
    {
        yield return new WaitForSeconds(delay);

        float elapsedTime = 0;
        var startSize = transform.localScale;
        var endSize = new Vector3(transform.localScale.x * scale, transform.localScale.y * scale, transform.localScale.z * scale);
        while (elapsedTime < seconds)
        {
            transform.localScale = Vector3.Lerp(startSize, endSize, curve.Evaluate(elapsedTime / seconds));
            elapsedTime += Time.deltaTime;
            yield return new WaitForEndOfFrame();
        }
        transform.localScale = endSize;
    }

    public static IEnumerator RotateOverSeconds(Transform transform, Quaternion rotationAmount, float seconds, float delay, AnimationCurve curve)
    {
        yield return new WaitForSeconds(delay);

        float elapsedTime = 0;
        var startRotation = transform.rotation;
        while (elapsedTime < seconds)
        {
            transform.rotation = Quaternion.Lerp(startRotation, rotationAmount, curve.Evaluate(elapsedTime / seconds));
            elapsedTime += Time.deltaTime;
            yield return new WaitForEndOfFrame();
        }
        transform.rotation = rotationAmount; // TODO: is this necessary?
    }

    public static IEnumerator MoveOverSecondsWithDepth(Transform transform, Vector3 endPos, float seconds, float delay, float depth, AnimationCurve curve)
    {
        // TODO: allow depth speed to be passed in
        float depthAnimationSeconds = 0.15f;

        float startDepth = transform.position.z;
        Vector3 startPosWithDepth = new Vector3(transform.position.x, transform.position.y, startDepth - depth);
        Vector3 endPosWithDepth = new Vector3(endPos.x, endPos.y, startDepth - depth);

        yield return MoveOverSeconds(transform, startPosWithDepth, depthAnimationSeconds, 0, curve);
        yield return MoveOverSeconds(transform, endPosWithDepth, seconds, delay, curve);
        yield return MoveOverSeconds(transform, endPos, depthAnimationSeconds, 0, curve);
    }

    public static IEnumerator MoveOverSecondsWithDepthReturn(Transform transform, Vector3 endPos, float seconds, float delay, float depthOffset, AnimationCurve curve)
    {
        // TODO: allow depth speed to be passed in
        float depthAnimationSeconds = 0.15f;

        float startDepth = transform.position.z;
        Vector3 startPosWithDepth = new Vector3(transform.position.x, transform.position.y, startDepth);
        Vector3 endPosWithDepth = new Vector3(endPos.x, endPos.y, startDepth - depthOffset);

        yield return MoveOverSeconds(transform, startPosWithDepth, depthAnimationSeconds, 0, curve);
        yield return MoveOverSeconds(transform, endPosWithDepth, seconds, delay, curve);
        yield return MoveOverSeconds(transform, endPos, depthAnimationSeconds, 0, curve);
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
