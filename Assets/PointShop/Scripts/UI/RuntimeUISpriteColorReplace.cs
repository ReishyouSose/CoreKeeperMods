using UnityEngine;

namespace Assets.PointShop.Scripts.UI
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class RuntimeUISpriteColorReplace : MonoBehaviour
    {
        private static Shader cachedShader;
        private Material runtimeMaterial;

        private void Awake()
        {
            if (cachedShader == null)
                cachedShader = Shader.Find("Amplify/UISpriteColorReplace");

            if (cachedShader == null)
            {
                Debug.LogError("[RuntimeUISpriteColorReplace] Shader not found.");
                return;
            }

            var sr = GetComponent<SpriteRenderer>();

            runtimeMaterial = new Material(cachedShader);

            var original = sr.sharedMaterial;
            if (original != null && original.mainTexture != null)
                runtimeMaterial.mainTexture = original.mainTexture;

            sr.sharedMaterial = runtimeMaterial;
        }

        private void OnDestroy()
        {
            if (runtimeMaterial != null)
                Destroy(runtimeMaterial);
        }
    }
}