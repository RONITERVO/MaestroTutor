// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
namespace Maestro.Quest.Imports
{
    public sealed partial class ImportWorkshop
    {
        void StopTutorPreview()
        {
            motionRequest++;
            if(animationWorkshop&&animationWorkshop.ControlsTarget("maestro")&&animationWorkshop.IsImportedPreview)animationWorkshop.Stop();
        }
        void StopSelectedPreview()
        {
            if(libraryMode){StopTutorPreview();return;}
            if(HasPreview){preview.Stop();return;}
            if(editor&&editor.SelectedId=="maestro"){StopTutorPreview();return;}
            if(editor)Target?.StopPreview();
        }
    }
}
