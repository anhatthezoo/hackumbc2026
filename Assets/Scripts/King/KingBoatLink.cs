using System;
using UnityEngine;

namespace RoyaltyBoat.King
{
    /// <summary>
    /// Integration boundary between the physical King and the boat/building system.
    /// The building system owns support detection and calls this component; the King
    /// is never parented or welded to the supplied Rigidbody.
    /// </summary>
    public sealed class KingBoatLink : MonoBehaviour
    {
        [SerializeField] private Rigidbody currentBoatBody;
        [SerializeField] private bool isSupported;

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
    }
}
