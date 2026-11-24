using UnityEngine;
namespace Casebook {
public class CasebookPickup : MonoBehaviour {
 public CasebookController journal;
 public string evidenceId;
 public void Collect(){if(journal!=null&&journal.View==DeskView.Closed)journal.Collect(evidenceId);}
}
}
