namespace DmiCad;
sealed partial class ModelingWindow
{
    void RefreshPlaneLink()
    {
        var source=document.Features.FirstOrDefault(f=>f.Id==pendingPlaneSourceId);
        planeLink.Enabled=source!=null;
        plane.Enabled=offset.Enabled=!planeLink.Checked;
        planeLinkInfo.Text=source?.Name??"고정 평면 위치";
        tips.SetToolTip(planeLinkInfo,source==null?"면 선택에서 유일한 돌출 끝 평면을 찾으면 연결할 수 있습니다.":"참조: "+source.Name+"의 끝 평면. 연결 해제 시 현재 위치를 고정합니다.");
        if(planeLink.Checked&&source!=null)
        {
            double value=document.EndOffset(source);
            if(value>=(double)offset.Minimum&&value<=(double)offset.Maximum)offset.Value=(decimal)value;
        }
    }
}
