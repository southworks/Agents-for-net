﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.SharePoint.Models
{
    /// <summary>
    /// SharePoint property pane choice group option object.
    /// </summary>
    public class PropertyPaneChoiceGroupOption
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PropertyPaneChoiceGroupOption"/> class.
        /// </summary>
        public PropertyPaneChoiceGroupOption()
        {
            // Do nothing
        }

        /// <summary>
        /// Gets or Sets optional ariaLabel flag. Text for screen-reader to announce regardless of toggle state. Of type <see cref="string"/>.
        /// </summary>
        /// <value>This value is the aria label of the choice group.</value>
        public string AriaLabel { get; set; }

        /// <summary>
        /// Gets or Sets a value indicating whether the property pane choice group option is checked or not of type <see cref="bool"/>.
        /// </summary>
        /// <value>This value indicates whether the control is checked.</value>
        public bool Checked { get; set; }

        /// <summary>
        /// Gets or Sets a value indicating whether this control is enabled or not of type <see cref="bool"/>.
        /// </summary>
        /// <value>This value indicates whether the control is disabled.</value>
        public bool Disabled { get; set; }

        /// <summary>
        /// Gets or Sets the Icon component props for choice field of type <see cref="PropertyPaneChoiceGroupIconProperties"/>.
        /// </summary>
        /// <value>This value is the icon properties of the choice group.</value>
        public PropertyPaneChoiceGroupIconProperties IconProps { get; set; }

        /// <summary>
        /// Gets or Sets the width and height of the image in px for choice field of type <see cref="PropertyPaneChoiceGroupImageSize"/>.
        /// </summary>
        /// <value>This value is the image size of the choice group.</value>
        public PropertyPaneChoiceGroupImageSize ImageSize { get; set; }

        /// <summary>
        /// Gets or Sets the src of image for choice field of type <see cref="string"/>.
        /// </summary>
        /// <value>This value is the image source of the choice group.</value>
        public string ImageSrc { get; set; }

        /// <summary>
        /// Gets or Sets a key to uniquely identify this option of type <see cref="string"/>.
        /// </summary>
        /// <value>This value is the key of the choice group.</value>
        public string Key { get; set; }

        /// <summary>
        /// Gets or Sets text to render for this option of type <see cref="string"/>.
        /// </summary>
        /// <value>This value is the text of the choice group.</value>
        public string Text { get; set; }
    }
}
