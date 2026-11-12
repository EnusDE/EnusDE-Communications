// Copyright (c) 2026 EnusDE. MIT License.
using UnityEngine;
using UnityEngine.UI;
namespace EnusDE.Communications
{
    // Cream line work like the native toolbar; geometry stays sharp at every UI scale.
    public sealed class ChatIcon:MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();var r=rectTransform.rect;
            Vector2 Point(float x,float y)=>new Vector2(r.xMin+x*r.width,r.yMin+y*r.height);
            void Line(float x1,float y1,float x2,float y2)
            {
                Vector2 a=Point(x1,y1),b=Point(x2,y2),normal=new Vector2(-(b-a).y,(b-a).x).normalized*Mathf.Max(.7f,r.width*.026f);
                int start=mesh.currentVertCount;mesh.AddVert(a-normal,color,Vector2.zero);mesh.AddVert(a+normal,color,Vector2.zero);mesh.AddVert(b+normal,color,Vector2.zero);mesh.AddVert(b-normal,color,Vector2.zero);mesh.AddTriangle(start,start+1,start+2);mesh.AddTriangle(start,start+2,start+3);
            }
            Line(.16f,.78f,.84f,.78f);Line(.84f,.78f,.84f,.32f);Line(.84f,.32f,.47f,.32f);Line(.47f,.32f,.29f,.16f);Line(.29f,.16f,.29f,.32f);Line(.29f,.32f,.16f,.32f);Line(.16f,.32f,.16f,.78f);
            Line(.28f,.62f,.72f,.62f);Line(.28f,.48f,.62f,.48f);
        }
    }
}
