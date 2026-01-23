using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class EventChoice //event geldiğinde oyuncuya sunulacak seçenek.
{
    public string text; 
    public List<SkillEffect> effects;
}

/*
ÖRNEK KULLANIM:

Event: "Bir memur sana rüşvet teklif ediyor"
├── Choice 1:
│   ├── text: "Kabul et"
│   └── effects: [Wealth +5000, Suspicion +15]
│
├── Choice 2:
│   ├── text: "Reddet"
│   └── effects: [Trust +10]
│
└── Choice 3:
    ├── text: "Polise ihbar et"
    └── effects: [Suspicion -10, Wealth -2000]
    
*/
