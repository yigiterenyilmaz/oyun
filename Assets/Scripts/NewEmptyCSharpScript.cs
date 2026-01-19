using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;


public class ImagePanner : MonoBehaviour, IPointerDownHandler, IDragHandler, IScrollHandler
// IPointerDownHandler, tıklama işlevini fonksiyon olarak eklemeye yarayan class   
// IDragHandler. tıklayıp sürükleme işlevini fonksiyon olarak eklemeye yarayan class falan işte
// !!! handler classları ui ögelerine has
{
    public float zoomSpeed = 0.02f; 
    private float baslangicxscale;
    private Vector3 mousePos;
      //zoom hızı
    public float minZoom = 20f;    // zoom alt sınır
    public float maxZoom = 32f;      //zoom üst sınır
    public RectTransform mapRect;    
    private Vector2 lastMousePosition;
    // tıklama işlemi yapıldığı an imlecin iki boyutlu konumunu tutar.
    // sürüklenirken imlecin mevcut konumu ile tıklandığı anki konumu (saklanan değişken) arasındaki fark
    // sayesinde haritanın sürüklenme miktarı ve yönü bulunur, sonra değişken mevcut konum ile güncellenir.
    private Canvas canvas;  

    
    
    void Awake(){
    
        canvas = GetComponentInParent<Canvas>(); // find parent canvas
    }
    
    void Start()    
    { 
        baslangicxscale=transform.localScale.x;
        canvas = GetComponentInParent<Canvas>();
    }

    private void Update()
    {
        
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        lastMousePosition=eventData.position;
        // tıklandığında sakla
    }

    public void OnScroll(PointerEventData eventData)
    {
     //imlece doğru zoom için önce kaydırma işlemi   
        
        
        
        //zoom
        
        float scroll = eventData.scrollDelta.y;
        float scale = transform.localScale.x;
       
        scale += scroll * zoomSpeed ;
        scale = Mathf.Clamp(scale, minZoom, maxZoom);

         float previousScale=transform.localScale.x;
        transform.localScale = new Vector3(scale, scale, 1f);

        if ((previousScale > 1)||(transform.localScale.x>1) &&  (previousScale <6)||(transform.localScale.x<6))
        {
            Vector2 mousePos = Mouse.current.position.ReadValue();
            
            transform.localPosition -= new Vector3((mousePos.x-960-transform.localPosition.x)*((transform.localScale.x-previousScale)/previousScale), (mousePos.y-540-transform.localPosition.y)*((transform.localScale.y-previousScale)/previousScale), 0);
        }
        checkBorders();
       

    }

    public void OnDrag(PointerEventData eventData)
    {
        Vector2 delta = eventData.position - lastMousePosition;
        Vector3 addition= new Vector3(delta.x,delta.y,0);
        transform.localPosition += addition / canvas.scaleFactor;
        lastMousePosition = eventData.position;
        checkBorders();
    }


    public void checkBorders()
    {
        Debug.Log("here");
        
        if ((transform.localPosition.x - (960 * transform.localScale.x)) >-960)
        {
            transform.localPosition= new Vector2((960 * transform.localScale.x)-960, transform.localPosition.y);
        }
        else if ((transform.localPosition.x + (960 * transform.localScale.x)) <960)
        {
            transform.localPosition= new Vector2(-(960 * transform.localScale.x)+960, transform.localPosition.y);
        } 
        
        if ((transform.localPosition.y - (540 * transform.localScale.y)) >-540)
        {
            transform.localPosition= new Vector2(transform.localPosition.x, (540 * transform.localScale.y)-540);
        }
        else if ((transform.localPosition.y + (540 * transform.localScale.y)) <540)
        {
            transform.localPosition= new Vector2(transform.localPosition.x, -(540 * transform.localScale.x)+540);
        } 
    }
    
    

    
}