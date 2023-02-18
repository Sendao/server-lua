/*
namespace CNet
{
	public class CNetEvents : MonoBehaviour
	{
		GameObject targetPlayer;

		public void Start()
		{
			targetPlayer = gameObject;
			
			EventHandler.UnregisterEvent<Ability, bool>(targetPlayer, "OnCharacterAbilityActive", OnAbilityActive);
            EventHandler.UnregisterEvent<ItemAbility, bool>(targetPlayer, "OnCharacterItemAbilityActive", OnItemAbilityActive);
		}

        private void OnAbilityActive(Ability ability, bool active)
        {
            // When an ability starts or stops it can prevent the camera from zooming.
            TryZoom(m_ZoomInput);
        }

        private void OnItemAbilityActive(ItemAbility itemAbility, bool active)
        {
            // When an ability starts or stops it can prevent the camera from zooming.
            TryZoom(m_ZoomInput);
        }
	}
}
*/