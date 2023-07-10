using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "ScriptableObjects/Animation Definition", fileName = "AnimationDefinition")]
public class AnimationDefinition : ScriptableObject
{
    [Range(0.1f, 2)]
    public float Duration = 0.5f;
    [Range(0, 1)]
    public float Delay = 0;
    [Space]
    public AnimationCurve Curve;
}
