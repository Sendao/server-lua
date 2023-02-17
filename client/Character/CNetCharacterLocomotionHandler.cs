namespace CNet
{
    using Opsive.Shared.Input;
    using Opsive.UltimateCharacterController.Character;
    using Opsive.UltimateCharacterController.Networking;
    using Opsive.UltimateCharacterController.Camera;
    using UnityEngine;

    public class CNetCharacterLocomotionHandler : UltimateCharacterLocomotionHandler
    {
        [SerializeField]
		protected bool use_camera = false;

        private INetworkInfo networkInfo;        
		[System.NonSerialized]
		private PlayerInput playerInput;

        protected override void Awake()
        {
            base.Awake();
            networkInfo = GetComponent<INetworkInfo>();
            playerInput = GetComponent<PlayerInput>();
        }

        private void Start()
        {
            if( networkInfo.IsLocalPlayer() ) {
                if( use_camera ) {
                    Debug.Log("Try to use camera");
                    var cam = Opsive.Shared.Camera.CameraUtility.FindCamera(gameObject);
                    if( cam ) {
                        GetComponent<Camera>().GetComponent<CameraController>().Character = gameObject;
                        Debug.Log("Used camera");
                    }
                }
            } else {
                Debug.Log("Disable player input");
                enabled = false;
                m_CharacterLocomotion.enabled = false;
                //playerInput.enabled = false;
            }
        }

        protected override void OnRespawn()
        {
            if (!networkInfo.IsLocalPlayer()) {
                return;
            }
            base.OnRespawn();
        }
    }
}