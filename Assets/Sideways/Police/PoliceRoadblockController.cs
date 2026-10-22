using UnityEngine;

namespace Sideways.Police
{
    public sealed class PoliceRoadblockController : MonoBehaviour
    {
        [SerializeField] private GameObject roadblockRoot;

        public bool IsDeployed { get; private set; }

        private void Awake()
        {
            roadblockRoot ??= gameObject;
            SetDeployed(false);
        }

        public void Configure(GameObject root)
        {
            roadblockRoot = root;
            SetDeployed(false);
        }

        public void SetDeployed(bool deployed)
        {
            IsDeployed = deployed;
            if (roadblockRoot == null || roadblockRoot == gameObject) SetChildrenActive(deployed);
            else roadblockRoot.SetActive(deployed);
        }

        private void SetChildrenActive(bool active)
        {
            for (var i = 0; i < transform.childCount; i++) transform.GetChild(i).gameObject.SetActive(active);
        }
    }
}
