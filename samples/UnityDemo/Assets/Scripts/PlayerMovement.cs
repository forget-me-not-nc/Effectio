using UnityEngine;

namespace EffectioDemo
{
    /// <summary>
    /// Top-down WASD / arrow-key movement. Speed comes from the entity's
    /// <c>Speed</c> Effectio stat, so any modifier (Hasted, Slowed, ...) that
    /// touches Speed instantly changes how fast the player walks.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerMovement : MonoBehaviour
    {
        [SerializeField] float _fallbackSpeed = 6f;

        Rigidbody _rb;
        CharacterStats _stats;

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.isKinematic = true;
            _rb.useGravity = false;
            _rb.constraints = RigidbodyConstraints.FreezeRotation;
            _stats = GetComponent<CharacterStats>();
        }

        void FixedUpdate()
        {
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            var dir = new Vector3(h, 0f, v);
            if (dir.sqrMagnitude > 1f) dir.Normalize();

            float speed = _fallbackSpeed;
            if (_stats != null && _stats.Entity != null && _stats.Entity.HasStat("Speed"))
                speed = _stats.Entity.GetStat("Speed").CurrentValue;

            var step = dir * (speed * Time.fixedDeltaTime);
            if (step.sqrMagnitude > 0f)
                _rb.MovePosition(_rb.position + step);
        }
    }
}

