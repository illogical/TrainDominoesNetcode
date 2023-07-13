using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Mover : MonoBehaviour
{
    public IEnumerator MoveOverSeconds(Vector3 destination, AnimationDefinition animationDefinition, Action afterComplete = null)
    {
        if (animationDefinition.Delay > 0)
        {
            yield return new WaitForSeconds(animationDefinition.Delay); // TODO: store in dictionary for performance's sake
        }

        float elapsedTime = 0;
        var startPos = transform.position;
        while (elapsedTime < animationDefinition.Duration)
        {
            transform.position = Vector3.Lerp(startPos, destination, animationDefinition.Curve.Evaluate(elapsedTime / animationDefinition.Duration));
            elapsedTime += Time.deltaTime;
            yield return new WaitForEndOfFrame();
        }
        transform.position = destination;

        if (afterComplete != null)
        {
            afterComplete();
        }
    }

    public IEnumerator MoveOverSeconds(Vector3 endPos, float seconds, float delay, AnimationCurve animationCurve, Action afterComplete = null)
    {
        if (delay > 0)
        {
            yield return new WaitForSeconds(delay); // TODO: store in dictionary for performance's sake
        }

        float elapsedTime = 0;
        var startPos = transform.position;
        while (elapsedTime < seconds)
        {
            transform.position = Vector3.Lerp(startPos, endPos, animationCurve.Evaluate(elapsedTime / seconds));
            elapsedTime += Time.deltaTime;
            yield return new WaitForEndOfFrame();
        }
        transform.position = endPos;

        if (afterComplete != null)
        {
            afterComplete();
        }
    }

    public IEnumerator RotateOverSeconds(Quaternion rotationAmount, AnimationDefinition animationDefinition)
    {
        yield return new WaitForSeconds(animationDefinition.Delay);

        float elapsedTime = 0;
        var startRotation = transform.rotation;
        while (elapsedTime < animationDefinition.Duration)
        {
            transform.rotation = Quaternion.Lerp(startRotation, rotationAmount, animationDefinition.Curve.Evaluate(elapsedTime / animationDefinition.Duration));
            elapsedTime += Time.deltaTime;
            yield return new WaitForEndOfFrame();
        }
        transform.rotation = rotationAmount;
    }

    public IEnumerator RotateOverSeconds(Quaternion rotationAmount, float seconds, AnimationCurve animationCurve, float delay)
    {
        yield return new WaitForSeconds(delay);

        float elapsedTime = 0;
        var startRotation = transform.rotation;
        while (elapsedTime < seconds)
        {
            transform.rotation = Quaternion.Lerp(startRotation, rotationAmount, animationCurve.Evaluate(elapsedTime / seconds));
            elapsedTime += Time.deltaTime;
            yield return new WaitForEndOfFrame();
        }
        transform.rotation = rotationAmount;
    }

    public IEnumerator SlideToPosition(Transform objectTransform, Vector3 endPos, AnimationDefinition animationDefinition)
    {
        if (objectTransform.position == endPos)
        {
            // skip objects that are already in place
            yield break;
        }

        yield return StartCoroutine(AnimationHelper.MoveOverSeconds(objectTransform, endPos, animationDefinition));
    }

    public IEnumerator SlideToPositionByIndex(int index, Vector3 destination, float animationDuration, float delayBeforeStart, float delayStagger, AnimationDefinition animationDefinition)
    {
        yield return StartCoroutine(AnimationHelper.MoveOverSeconds(transform, destination, animationDuration, index * delayStagger + delayBeforeStart, animationDefinition));
    }
}
