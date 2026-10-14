using UnityEngine;

namespace Foundry.Fracture
{
    // What a broken-off piece does after the break (added by Fracturable):
    //  - ShrinkAway: tumbles for Lifetime seconds, then shrinks to nothing and is removed
    //  - StayForever: stays a physics object
    //  - SettleThenFreeze: once it comes to rest, its Rigidbody is frozen (kinematic) - rubble that stays put for free
    public class FractureChunk : MonoBehaviour
    {
        public enum Debris
        {
            ShrinkAway,
            StayForever,
            SettleThenFreeze
        }

        private const float SETTLE_TIME = 0.5f;

        private Debris _behaviour;
        private float _lifetime;
        private float _shrinkTime;
        private float _settleSpeed;
        private float _age;
        private float _stillFor;
        private Vector3 _startScale;
        private Rigidbody _body;

        public void Initialise(Debris behaviour, float lifetime, float shrinkTime, float settleSpeed)
        {
            _behaviour = behaviour;
            _lifetime = lifetime;
            _shrinkTime = Mathf.Max(0.01f, shrinkTime);
            _settleSpeed = settleSpeed;
            _startScale = transform.localScale;
            _body = GetComponent<Rigidbody>();
        }

        private void Update()
        {
            if (_behaviour != Debris.ShrinkAway)
                return;

            _age += Time.deltaTime;
            if (_age < _lifetime)
                return;

            float t = (_age - _lifetime) / _shrinkTime;
            if (t >= 1f)
            {
                Destroy(gameObject);
                return;
            }

            transform.localScale = _startScale * (1f - t);
        }

        private void FixedUpdate()
        {
            if (_behaviour != Debris.SettleThenFreeze || _body == null || _body.isKinematic)
                return;

            bool still = _body.linearVelocity.sqrMagnitude < _settleSpeed * _settleSpeed && _body.angularVelocity.sqrMagnitude < _settleSpeed * _settleSpeed;
            _stillFor = still ? _stillFor + Time.fixedDeltaTime : 0f;
            if (_stillFor >= SETTLE_TIME)
            {
                _body.isKinematic = true;
                enabled = false;
            }
        }
    }
}
