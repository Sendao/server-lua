using Opsive.UltimateCharacterController.Objects;

namespace CNet
{
    public class CNetObjId : ObjectIdentifier
    {
        private bool registered;

        private void Awake()
        {
            if (GetComponent<CNetId>() == null) {
                NetSocket.Instance.RegisterObjectIdentifier(this);
                registered = true;
            }
        }

        private void OnDestroy()
        {
            if (registered) {
                NetSocket.Instance.UnregisterObjectIdentifier(this);
                registered = false;
            }
        }
    }
}