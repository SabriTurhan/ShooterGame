using UnityEngine;

namespace Watermelon
{
    [System.Serializable]
    public class CameraLocalData
    {
        [SerializeField] float fieldOfView = 60f;
        public float FieldOfView
        {
            get { return fieldOfView; }
            set { fieldOfView = value; }
        }

        [SerializeField] float nearClipPlane = 0.1f;
        public float NearClipPlane
          {
            get { return nearClipPlane; }
            set { nearClipPlane = value; }
        }

        [SerializeField] float farClipPlane = 100f;
        public float FarClipPlane
          {
            get { return farClipPlane; }
            set { farClipPlane = value; }
        }

        [Space]
        [SerializeField] Vector3 followOffset;
        public Vector3 FollowOffset
        {
            get { return followOffset; }
            set { followOffset = value; }
        }

        [SerializeField] Vector3 simpleRotation;
        public Vector3 SimpleRotation
        {
            get { return simpleRotation; }
            set { simpleRotation = value; }
        }

        public Vector3 Position { get; private set; }
        public Quaternion Rotation { get; private set; }

        public CameraLocalData() 
        {
            Position = followOffset;
            Rotation = Quaternion.Euler(simpleRotation);
        }

        public CameraLocalData(CameraLocalData cameraData) : this()
        {
            FieldOfView = cameraData.FieldOfView;
            NearClipPlane = cameraData.NearClipPlane;
            FarClipPlane = cameraData.FarClipPlane;

            FollowOffset = cameraData.FollowOffset;
            SimpleRotation = cameraData.SimpleRotation;
        }

        public void Lerp(CameraLocalData start, CameraLocalData target, float t)
        {
            FieldOfView = Mathf.Lerp(start.FieldOfView, target.FieldOfView, t);
            NearClipPlane = Mathf.Lerp(start.NearClipPlane, target.NearClipPlane, t);
            FarClipPlane = Mathf.Lerp(start.FarClipPlane, target.FarClipPlane, t);

            FollowOffset = Vector3.Lerp(start.FollowOffset, target.FollowOffset, t);
            SimpleRotation = Vector3.Lerp(start.SimpleRotation, target.SimpleRotation, t);

            Position = Vector3.Lerp(start.Position, target.Position, t);
            Rotation = Quaternion.Slerp(start.Rotation, target.Rotation, t);
        }

        public void UpdatePosition(Vector3 newValue)
        {
            Position = newValue;
        }

        public void UpdateRotation(Quaternion newValue)
        {
            Rotation = newValue;
        }
    }
}