// Copyright (c) 2026 EnusDE. MIT License.
using UnityEngine;
using UnityEngine.UI;

namespace EnusDE.Communications
{
    public sealed class MicrophoneIcon:MaskableGraphic
    {
        static readonly Color Amber=new Color(1f,.79f,.35f);
        static readonly Color Green=new Color(.42f,.91f,.47f);
        static readonly Color Red=new Color(1f,.37f,.32f);
        MicrophoneIndicator state;

        internal void SetState(MicrophoneIndicator next)
        {
            if(state==next)return;
            state=next;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect=rectTransform.rect;
            Color ink=state==MicrophoneIndicator.Live?Green:Amber;

            Vector2 Point(float x,float y)=>new Vector2(rect.xMin+x*rect.width,rect.yMin+y*rect.height);

            void Line(Vector2 start,Vector2 end,Color tint)
            {
                Vector2 diff=end-start;
                Vector2 normal=new Vector2(-diff.y,diff.x).normalized*Mathf.Max(.65f,rect.width*.032f);
                int index=mesh.currentVertCount;
                mesh.AddVert(start-normal,tint,Vector2.zero);
                mesh.AddVert(start+normal,tint,Vector2.zero);
                mesh.AddVert(end+normal,tint,Vector2.zero);
                mesh.AddVert(end-normal,tint,Vector2.zero);
                mesh.AddTriangle(index,index+1,index+2);
                mesh.AddTriangle(index,index+2,index+3);
            }

            void Arc(float x,float y,float radius,float from,float to,Color tint)
            {
                int segments=Mathf.CeilToInt((to-from)/15);
                Vector2 At(float degrees)
                {
                    float radians=degrees*Mathf.Deg2Rad;
                    return Point(x+radius*Mathf.Cos(radians),y+radius*Mathf.Sin(radians));
                }
                Vector2 last=At(from);
                for(int i=1;i<=segments;i++)
                {
                    Vector2 next=At(Mathf.Lerp(from,to,(float)i/segments));
                    Line(last,next,tint);
                    last=next;
                }
            }

            Arc(.5f,.72f,.115f,0,180,ink);
            Arc(.5f,.52f,.115f,180,360,ink);
            Line(Point(.385f,.72f),Point(.385f,.52f),ink);
            Line(Point(.615f,.72f),Point(.615f,.52f),ink);
            Arc(.5f,.51f,.20f,180,360,ink);
            Line(Point(.5f,.31f),Point(.5f,.18f),ink);
            Line(Point(.365f,.18f),Point(.635f,.18f),ink);

            if(state==MicrophoneIndicator.Unavailable)
            {
                Arc(.5f,.5f,.42f,0,360,Red);
                Line(Point(.204f,.796f),Point(.796f,.204f),Red);
            }
        }
    }
}
