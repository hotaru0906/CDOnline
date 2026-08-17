using UnityEngine;
using System.Collections;

public class panelroom : MonoBehaviour
{
   private Animator animator;

    void Awake()
    {
        animator = GetComponent<Animator>();
    }

    public void OpenPanel()
    {
        gameObject.SetActive(true);
        animator.Play("Open");
    }

    public void ClosePanel()
    {
        animator.SetTrigger("Close");
    }

    // Gọi ở frame cuối của animation Close
    public void DisablePanel()
    {
        gameObject.SetActive(false);
    }
}