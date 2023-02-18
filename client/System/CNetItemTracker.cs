using Opsive.Shared.Inventory;
using Opsive.UltimateCharacterController.Inventory;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CNetItemTracker : MonoBehaviour
{
	private static CNetItemTracker instance=null;

	[Tooltip("A reference to all of the available ItemIdentifiers.")]
	[SerializeField] protected ItemCollection m_ItemCollection;

	public ItemCollection ItemCollection { get { return m_ItemCollection; } set { m_ItemCollection = value; } }
	private Dictionary<uint, IItemIdentifier> m_IDItemIdentifierMap = new Dictionary<uint, IItemIdentifier>();

	private void Awake()
	{
		instance = this;

		if (m_ItemCollection == null || m_ItemCollection.ItemTypes == null) {
			return;
		}

		for (int i = 0; i < m_ItemCollection.ItemTypes.Length; ++i) {
			m_IDItemIdentifierMap.Add(m_ItemCollection.ItemTypes[i].ID, m_ItemCollection.ItemTypes[i]);
		}
	}

	public static IItemIdentifier GetItem(uint id)
	{
		return instance.GetItemIdentifierInternal(id);
	}

	private IItemIdentifier GetItemIdentifierInternal(uint id)
	{
		if (m_IDItemIdentifierMap.TryGetValue(id, out var itemIdentifier)) {
			return itemIdentifier;
		}
		return null;
	}
}
