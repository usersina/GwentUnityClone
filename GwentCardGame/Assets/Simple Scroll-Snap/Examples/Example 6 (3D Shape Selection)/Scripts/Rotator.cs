using UnityEngine;

namespace DanielLochner.Assets.SimpleScrollSnap
{
    public class Rotator : MonoBehaviour
    {
        // UserSina Added: 0f
        [SerializeField] private float rotationSpeed = 0f;

        private void Update()
        {
            transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
        }
    }
}