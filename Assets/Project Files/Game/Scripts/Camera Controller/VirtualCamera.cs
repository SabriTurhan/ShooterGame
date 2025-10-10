using UnityEngine;

namespace Watermelon
{
    [System.Serializable]
    public sealed class VirtualCamera : MonoBehaviour
    {
        [SerializeField, ReadOnly] bool isActive;
        public bool IsActive => isActive;

        [SerializeField] CameraType cameraType;
        public CameraType CameraType => cameraType;

        [SerializeField] CameraLocalData cameraData;

        private bool isShaking;
        private float shakeGain = 0.0f;
        private TweenCase shakeTweenCase;

        private bool isBlending;
        public bool IsBlending => isBlending;

        public CameraLocalData CameraData => cameraData;

        private Transform target;
        public Transform Target => target;

        public void Init()
        {
            isActive = false;
            isBlending = false;
        }

        public void SetTarget(Transform target)
        {
            this.target = target;
        }

        public void SetFollowOffset(Vector3 followOffset)
        {
            cameraData.FollowOffset = followOffset;
        }

        public void SetSimpleRotation(Vector3 rotation)
        {
            cameraData.SimpleRotation = rotation;

            cameraData.UpdateRotation(Quaternion.Euler(cameraData.SimpleRotation));
        }

        public void SetFov(float fOV)
        {
            cameraData.FieldOfView = fOV;
        }

        private void LateUpdate()
        {
            if ((!isActive && !isBlending) || target == null)
                return;

            if (isShaking)
            {
                // Recalculate camera position
                cameraData.UpdatePosition(target.position + cameraData.FollowOffset + (Random.onUnitSphere * shakeGain * Time.deltaTime));
            }
            else
            {
                // Recalculate camera position
                cameraData.UpdatePosition(target.position + cameraData.FollowOffset);
            }

            cameraData.UpdateRotation(Quaternion.Euler(cameraData.SimpleRotation));
        }

        public void StartTransition()
        {
            isBlending = true;
        }

        public void StopTransition()
        {
            isBlending = false;
        }

        public void Activate()
        {
            isActive = true;
        }

        public void Disable()
        {
            isActive = false;
        }

        public void Shake(float fadeInTime, float fadeOutTime, float duration, float gain)
        {
            if (isShaking)
                return;

            isShaking = true;

            if (shakeTweenCase != null && !shakeTweenCase.IsCompleted)
                shakeTweenCase.Kill();

            shakeGain = 0;

            shakeTweenCase = Tween.DoFloat(0.0f, gain, fadeInTime, (float fadeInValue) =>
            {
                shakeGain = fadeInValue;
            }).OnComplete(delegate
            {
                shakeTweenCase = Tween.DelayedCall(duration, delegate
                {
                    shakeTweenCase = Tween.DoFloat(gain, 0.0f, fadeOutTime, (float fadeOutValue) =>
                    {
                        shakeGain = fadeOutValue;

                        isShaking = false;
                    });
                });
            });
        }
    }
}