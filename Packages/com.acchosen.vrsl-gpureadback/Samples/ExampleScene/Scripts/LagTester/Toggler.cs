using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using UdonSharp;

namespace VRSL.GPUReadBack.Samples
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class Toggler : UdonSharpBehaviour
    {
        public GameObject target;

        public override void Interact()
        {
            bool toggle = !target.activeSelf;
            target.SetActive(toggle);
        }
    }
}