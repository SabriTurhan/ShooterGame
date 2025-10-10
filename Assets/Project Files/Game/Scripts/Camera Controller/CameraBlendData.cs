namespace Watermelon
{
    [System.Serializable]
    public class CameraBlendData
    {
        public float BlendTime = 0.5f;
        public Ease.Type BlendEaseType = Ease.Type.SineInOut;

        public CameraBlendData(float blendTime, Ease.Type blendEaseType)
        {
            BlendTime = blendTime;
            BlendEaseType = blendEaseType;
        }

        public void OverrideBlendTime(float newValue)
        {
            BlendTime = newValue;
        }
    }
}