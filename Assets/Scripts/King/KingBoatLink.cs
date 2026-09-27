using System;
using UnityEngine;

namespace RoyaltyBoat.King
{
    /// <summary>
    /// Integration boundary between the physical King and the boat/building system.
    /// The building system owns support detection and calls this component. During
    /// building it records the boat relationship; during Voyage it uses a fixed joint
    /// so the King follows the boat's translation, rotation, and wave motion.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class KingBoatLink : MonoBehaviour
    {
        [SerializeField] private Rigidbody currentBoatBody;
        [SerializeField] private bool isSupported;

        private Rigidbody kingBody;
        private FixedJoint boatJoint;

        public Rigidbody CurrentBoatBody => currentBoatBody;
        public Transform CurrentBoatRoot => currentBoatBody == null ? null : currentBoatBody.transform;
        public bool IsConnected => currentBoatBody != null;
        public bool IsSupported => IsConnected && isSupported;

        public event Action<Rigidbody> BoatChanged;
        public event Action<bool> SupportChanged;

        public void Connect(Rigidbody boatBody, bool supported = true)
        {
            if (boatBody == null)
            {
                throw new ArgumentNullException(nameof(boatBody));
            }

            bool boatWasChanged = currentBoatBody != boatBody;
            currentBoatBody = boatBody;

            if (boatWasChanged)
            {
                BoatChanged?.Invoke(currentBoatBody);
            }

            RefreshBoatJoint();
            SetSupported(supported);
        }

        public void SetSupported(bool supported)
        {
            bool nextValue = currentBoatBody != null && supported;
            if (isSupported == nextValue)
            {
                return;
            }

            isSupported = nextValue;
            SupportChanged?.Invoke(isSupported);
        }

        public void Disconnect(Rigidbody expectedBoatBody = null)
        {
            if (expectedBoatBody != null && currentBoatBody != expectedBoatBody)
            {
                return;
            }

            bool supportWasActive = isSupported;
            bool hadBoat = currentBoatBody != null;
            RemoveBoatJoint();
            currentBoatBody = null;
            isSupported = false;

            if (hadBoat)
            {
                BoatChanged?.Invoke(null);
            }

            if (supportWasActive)
            {
                SupportChanged?.Invoke(false);
            }
        }

        public bool TryGetBoatBody(out Rigidbody boatBody)
        {
            boatBody = currentBoatBody;
            return boatBody != null;
        }

        private void RefreshBoatJoint()
        {
            kingBody ??= GetComponent<Rigidbody>();
            if (kingBody == null || kingBody.isKinematic || currentBoatBody == null)
            {
                RemoveBoatJoint();
                return;
            }

            if (boatJoint == null)
            {
                boatJoint = gameObject.AddComponent<FixedJoint>();
            }

            boatJoint.connectedBody = currentBoatBody;
            boatJoint.autoConfigureConnectedAnchor = true;
            boatJoint.enableCollision = false;
            boatJoint.breakForce = Mathf.Infinity;
            boatJoint.breakTorque = Mathf.Infinity;
        }

        private void RemoveBoatJoint()
        {
            if (boatJoint == null)
            {
                return;
            }

            boatJoint.connectedBody = null;
            if (Application.isPlaying)
            {
                Destroy(boatJoint);
            }
            else
            {
                DestroyImmediate(boatJoint);
            }

            boatJoint = null;
        }
    }
}
