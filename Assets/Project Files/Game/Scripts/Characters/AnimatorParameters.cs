using System.Collections.Generic;
using UnityEngine;

namespace Watermelon
{
    public class AnimatorParameters
    {
        private readonly Dictionary<string, float> floatParameters = new Dictionary<string, float>();
        private readonly Dictionary<string, int> intParameters = new Dictionary<string, int>();
        private readonly Dictionary<string, bool> boolParameters = new Dictionary<string, bool>();
        private readonly List<string> triggerParameters = new List<string>();
        private readonly Dictionary<int, float> layerWeights = new Dictionary<int, float>();

        public AnimatorParameters(Animator animator)
        {
            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                switch (parameter.type)
                {
                    case AnimatorControllerParameterType.Float:
                        floatParameters[parameter.name] = animator.GetFloat(parameter.name);
                        break;
                    case AnimatorControllerParameterType.Int:
                        intParameters[parameter.name] = animator.GetInteger(parameter.name);
                        break;
                    case AnimatorControllerParameterType.Bool:
                        boolParameters[parameter.name] = animator.GetBool(parameter.name);
                        break;
                    case AnimatorControllerParameterType.Trigger:
                        if (animator.GetBool(parameter.name))
                            triggerParameters.Add(parameter.name);
                        break;
                }
            }

            for (int i = 0; i < animator.layerCount; i++)
            {
                layerWeights[i] = animator.GetLayerWeight(i);
            }
        }

        public void ApplyTo(Animator animator)
        {
            foreach (var parameter in floatParameters)
            {
                animator.SetFloat(parameter.Key, parameter.Value);
            }

            foreach (var parameter in intParameters)
            {
                animator.SetInteger(parameter.Key, parameter.Value);
            }

            foreach (var parameter in boolParameters)
            {
                animator.SetBool(parameter.Key, parameter.Value);
            }

            foreach (var parameter in triggerParameters)
            {
                animator.SetTrigger(parameter);
            }

            foreach (var layerWeight in layerWeights)
            {
                animator.SetLayerWeight(layerWeight.Key, layerWeight.Value);
            }
        }
    }
}
