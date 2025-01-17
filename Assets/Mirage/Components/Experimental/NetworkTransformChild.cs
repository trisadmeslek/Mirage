﻿using UnityEngine;

namespace Mirage.Experimental
{
    /// <summary>
    /// A component to synchronize the position of child transforms of networked objects.
    /// <para>There must be a NetworkTransform on the root object of the hierarchy. There can be multiple NetworkTransformChild components on an object. This does not use physics for synchronization, it simply synchronizes the localPosition and localRotation of the child transform and lerps towards the received values.</para>
    /// </summary>
    [AddComponentMenu("Network/Experimental/NetworkTransformChildExperimentalExperimental")]
    [HelpURL("https://miragenet.github.io/Mirage/Articles/Components/NetworkTransformChild.html")]
    public class NetworkTransformChild : NetworkTransformBase
    {
        [Header("Target")]
        public Transform target;

        protected override Transform TargetTransform => target;
    }
}
