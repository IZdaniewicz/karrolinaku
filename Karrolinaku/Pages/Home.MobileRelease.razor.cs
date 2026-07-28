using Microsoft.AspNetCore.Components.Web;

namespace Karrolinaku.Pages;

public partial class Home
{
    private void HandlePointerUpWithAutoCancel(PointerEventArgs args, GridCell cell)
    {
        if (!IsTouchLikePointer(args))
        {
            HandlePointerUp(args, cell);
            return;
        }

        if (IsTouchStretching && ActiveTouchPointerId == args.PointerId)
        {
            UpdateTouchStretchFromPoint(args.ClientX, args.ClientY);

            bool accepted = TryAcceptCurrentRect();

            TapStartCell = null;
            CurrentRect = null;
            ResetTouchStretchState();
            ResetTouchPointer();

            if (!accepted)
                StatusMessage += " Wybór został anulowany — wskaż nowy punkt początkowy.";

            PaintGrid();
            return;
        }

        HandleTouchPointerUp(args);
    }
}
