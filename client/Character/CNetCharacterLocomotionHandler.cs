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

        private CNetId cni;

        private INetworkInfo networkInfo;        
		[System.NonSerialized]
		private PlayerInput playerInput;

        protected override void Awake()
        {
            base.Awake();
            cni = GetComponent<CNetId>();
            playerInput = GetComponent<PlayerInput>();
        }

        private void Start()
        {
            if( cni.local ) {
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

                UltimateCharacterLocomotionHandler[] handlers = GetComponents<UltimateCharacterLocomotionHandler>();
                int i;
                for( i=0; i<handlers.Length; i++ ) {
                    if( handlers[i] is CNetCharacterLocomotionHandler ) {
                        continue;
                    }
                    Destroy( handlers[i] );
                }
                //playerInput.enabled = false;
                //this.enabled = false;
                Debug.Log("Self enabled!");
                //m_CharacterLocomotion.enabled = false;
                //playerInput.enabled = false;
                //enabled = false;
            }
        }

        protected override void OnRespawn()
        {
            if (!cni.local) {
                return;
            }
            base.OnRespawn();
        }
    }
}