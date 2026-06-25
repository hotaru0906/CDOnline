using TMPro;
using UnityEngine;
using System.Collections.Generic;

public class MinigamePodiumUI : MonoBehaviour
{
    [Header("Names")]
    public TMP_Text rank1Name;
    public TMP_Text rank2Name;
    public TMP_Text rank3Name;

    [Header("Preview Anchors")]
    public Transform rank1Anchor;
    public Transform rank2Anchor;
    public Transform rank3Anchor;

    [Header("Character Prefabs")]
    [SerializeField] private GameObject[] characterPrefabs;

    private void OnEnable()
    {
        RefreshPodium();
        Debug.Log("PODIUM ENABLE");
    }

    private void ClearAnchor(Transform anchor)
    {
        foreach (Transform child in anchor)
        {
            Destroy(child.gameObject);
        }
    }

    private void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;

        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

   private void SpawnModel(int characterIndex, Transform anchor)
    {
        if (characterIndex < 0 ||
            characterIndex >= characterPrefabs.Length)
            return;

        GameObject model =
            Instantiate(
                characterPrefabs[characterIndex],
                anchor,
                false);

        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation =
            Quaternion.Euler(0f, 180f, 0f);
        model.transform.localScale =
            new Vector3(20f, 20f, 20f);

        SetLayerRecursively(
            model,
            LayerMask.NameToLayer("PreviewCharacter"));
    }

    public void RefreshPodium()
    {

        Debug.Log("REFRESH PODIUM");
        
        if (ScoreboardManager.Instance == null)
            return;

        List<PlayerNetworkData> rankedPlayers =
            ScoreboardManager.Instance.GetRankedPlayers();

        ClearAnchor(rank1Anchor);
        ClearAnchor(rank2Anchor);
        ClearAnchor(rank3Anchor);

        //----------------------------------
        // TOP 1
        //----------------------------------

        if (rankedPlayers.Count > 0)
        {
            rank1Name.text =
                rankedPlayers[0].PlayerName.ToString();

            SpawnModel(
                rankedPlayers[0].CharacterIndex,
                rank1Anchor);
        }
        else
        {
            rank1Name.text = "";
        }

        //----------------------------------
        // TOP 2
        //----------------------------------

        if (rankedPlayers.Count > 1)
        {
            rank2Name.text =
                rankedPlayers[1].PlayerName.ToString();

            SpawnModel(
                rankedPlayers[1].CharacterIndex,
                rank2Anchor);
        }
        else
        {
            rank2Name.text = "";
        }

        //----------------------------------
        // TOP 3
        //----------------------------------

        if (rankedPlayers.Count > 2)
        {
            rank3Name.text =
                rankedPlayers[2].PlayerName.ToString();

            SpawnModel(
                rankedPlayers[2].CharacterIndex,
                rank3Anchor);
        }
        else
        {
            rank3Name.text = "";
        }
    }
}