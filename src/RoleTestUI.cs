// Copyright (c) 2026 EnusDE. MIT License.
using UnityEngine;
namespace EnusDE.Communications
{
    public sealed partial class CommsUI
    {
        void BuildRoleTestBar()
        {
            if(roleTestBar!=null)return;
            roleTestBar=f.Window(root,T("test_active"),800,110,entry.StopRoleTest);roleTestBar.anchoredPosition=new Vector2((root.rect.width-800)/2,-44);
            roleTestLabel=f.Text(roleTestBar,"",12,38,776,26,15);
            f.Button(roleTestBar,T("test_driver"),12,72,180,28,()=>entry.SwitchTestRole(TestChatRole.Driver));
            f.Button(roleTestBar,T("test_dispatcher"),204,72,180,28,()=>entry.SwitchTestRole(TestChatRole.Dispatcher));
            f.Button(roleTestBar,T("test_admin"),396,72,180,28,()=>entry.SwitchTestRole(TestChatRole.Administrator));
            f.Button(roleTestBar,T("test_stop"),588,72,200,28,entry.StopRoleTest);
        }
    }
}
