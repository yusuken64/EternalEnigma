using UnityEngine;
using UnityEngine.UI;

/// <summary>Authored grid rules that fit variable content to its container at any screen size.</summary>
[AddComponentMenu("Layout/Responsive Grid Layout")]
public sealed class ResponsiveGridLayout : LayoutGroup
{
    [Min(1)] public int Columns=2;
    public bool SingleRow;
    [Range(0, .2f)] public float HorizontalInset;
    [Range(0, .2f)] public float ColumnGap=.02f;
    [Range(0, 1)] public float RowFill=.88f;

    public override void CalculateLayoutInputHorizontal()
    {
        base.CalculateLayoutInputHorizontal();
        SetLayoutInputForAxis(0,0,1,0);
    }
    public override void CalculateLayoutInputVertical()=>SetLayoutInputForAxis(0,0,1,1);
    public override void SetLayoutHorizontal()=>Arrange(0);
    public override void SetLayoutVertical()=>Arrange(1);
    private void Arrange(int axis)
    {
        int columns=SingleRow?Mathf.Max(Columns,rectChildren.Count):Mathf.Max(1,Columns);
        int rows=SingleRow?1:Mathf.Max(1,Mathf.CeilToInt(rectChildren.Count/(float)columns));
        float width=rectTransform.rect.width-padding.horizontal;
        float height=rectTransform.rect.height-padding.vertical;
        float gap=width*ColumnGap;
        float cellWidth=Mathf.Max(0,(width*(1-2*HorizontalInset)-gap*(columns-1))/columns);
        float cellHeight=Mathf.Max(0,height/rows);
        for(int i=0;i<rectChildren.Count;i++)
        {
            if(axis==0)SetChildAlongAxis(rectChildren[i],0,padding.left+width*HorizontalInset+(i%columns)*(cellWidth+gap),cellWidth);
            else SetChildAlongAxis(rectChildren[i],1,padding.top+(i/columns)*cellHeight,cellHeight*RowFill);
        }
    }
}
