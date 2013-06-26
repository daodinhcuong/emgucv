﻿//----------------------------------------------------------------------------
//  Copyright (C) 2004-2013 by EMGU. All rights reserved.       
//----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using Emgu.CV.Structure;
using Emgu.CV;
using Emgu.CV.Util;

namespace Emgu.CV.OpenCL
{
   /// <summary>
   /// An OclImage is very similar to the Emgu.CV.Image except that it is being used for OpenCL processing
   /// </summary>
   /// <typeparam name="TColor">Color type of this image (either Gray, Bgr, Bgra, Hsv, Hls, Lab, Luv, Xyz, Ycc, Rgb or Rbga)</typeparam>
   /// <typeparam name="TDepth">Depth of this image (either Byte, SByte, Single, double, UInt16, Int16 or Int32)</typeparam>
   public class OclImage<TColor, TDepth>
      : OclMat<TDepth>
      where TColor : struct, IColor
      where TDepth : new()
   {
      /// <summary>
      /// Create an empty GpuImage
      /// </summary>
      public OclImage()
         : base()
      {
      }

      /// <summary>
      /// Create the GpuImage from the unmanaged pointer.
      /// </summary>
      /// <param name="ptr">The unmanaged pointer to the GpuMat. It is the user's responsibility that the Color type and depth matches between the managed class and unmanaged pointer.</param>
      public OclImage(IntPtr ptr)
         : base(ptr)
      {
      }

      /*
      /// <summary>
      /// Create a GPU image from a regular image
      /// </summary>
      /// <param name="img">The image to be converted to GPU image</param>
      public OclImage(Image<TColor, TDepth> img)
         : base(img)
      {
      }

      /// <summary>
      /// Create a OclImage of the specific size
      /// </summary>
      /// <param name="rows">The number of rows (height)</param>
      /// <param name="cols">The number of columns (width)</param>
      /// <param name="continuous">Indicates if the data should be continuous</param>
      public OclImage(int rows, int cols, bool continuous)
         : base(rows, cols, new TColor().Dimension, continuous)
      {
      }*/

      /// <summary>
      /// Create an OclImage of the specific size
      /// </summary>
      /// <param name="rows">The number of rows (height)</param>
      /// <param name="cols">The number of columns (width)</param>
      public OclImage(int rows, int cols)
         : base(rows, cols, new TColor().Dimension)
      {
      }

      /// <summary>
      /// Create a GpuImage of the specific size
      /// </summary>
      /// <param name="size">The size of the image</param>
      public OclImage(Size size)
         : this(size.Height, size.Width)
      {
      }

      /// <summary>
      /// Resize the OclImage. The calling OclMat be OclMat%lt;Byte&gt;. 
      /// </summary>
      /// <param name="size">The new size</param>
      /// <param name="interpolationType">The interpolation type</param>
      /// <returns>An OclImage of the new size</returns>
      public OclImage<TColor, TDepth> Resize(Size size, CvEnum.INTER interpolationType)
      {
         OclImage<TColor, TDepth> result = new OclImage<TColor, TDepth>(size);
         OclInvoke.Resize(_ptr, result, 0, 0, interpolationType);
         return result;
      }

      ///<summary> 
      ///Split current Image into an array of gray scale images where each element 
      ///in the array represent a single color channel of the original image
      ///</summary>
      ///<returns> 
      ///An array of gray scale images where each element  
      ///in the array represent a single color channel of the original image 
      ///</returns>
      public new OclImage<Gray, TDepth>[] Split()
      {
         OclImage<Gray, TDepth>[] result = new OclImage<Gray, TDepth>[NumberOfChannels];
         Size size = Size;
         for (int i = 0; i < result.Length; i++)
         {
            result[i] = new OclImage<Gray, TDepth>(size);
         }

         SplitInto(result);
         return result;
      }

      
      ///<summary> Convert the current GpuImage to the specific color and depth </summary>
      ///<typeparam name="TOtherColor"> The type of color to be converted to </typeparam>
      ///<typeparam name="TOtherDepth"> The type of pixel depth to be converted to </typeparam>
      ///<returns>GpuImage of the specific color and depth </returns>
      public OclImage<TOtherColor, TOtherDepth> Convert<TOtherColor, TOtherDepth>()
         where TOtherColor : struct, IColor
         where TOtherDepth : new()
      {
         OclImage<TOtherColor, TOtherDepth> res = new OclImage<TOtherColor, TOtherDepth>(Size);
         res.ConvertFrom(this);
         return res;
      }

      /// <summary>
      /// Convert the source image to the current image, if the size are different, the current image will be a resized version of the srcImage. 
      /// </summary>
      /// <typeparam name="TSrcColor">The color type of the source image</typeparam>
      /// <typeparam name="TSrcDepth">The color depth of the source image</typeparam>
      /// <param name="srcImage">The sourceImage</param>
      public void ConvertFrom<TSrcColor, TSrcDepth>(OclImage<TSrcColor, TSrcDepth> srcImage)
         where TSrcColor : struct, IColor
         where TSrcDepth : new()
      {
         if (!Size.Equals(srcImage.Size))
         {  //if the size of the source image do not match the size of the current image
            using (OclImage<TSrcColor, TSrcDepth> tmp = srcImage.Resize(Size, Emgu.CV.CvEnum.INTER.CV_INTER_LINEAR))
            {
               ConvertFrom(tmp);
               return;
            }
         }

         if (typeof(TColor) == typeof(TSrcColor))
         {
            #region same color
            if (typeof(TDepth) == typeof(TSrcDepth)) //same depth
            {
               OclInvoke.Copy(srcImage.Ptr, Ptr, IntPtr.Zero);
            }
            else //different depth
            {
               if (typeof(TDepth) == typeof(Byte) && typeof(TSrcDepth) != typeof(Byte))
               {
                  double[] minVal, maxVal;
                  Point[] minLoc, maxLoc;
                  srcImage.MinMax(out minVal, out maxVal, out minLoc, out maxLoc);
                  double min = minVal[0];
                  double max = maxVal[0];
                  for (int i = 1; i < minVal.Length; i++)
                  {
                     min = Math.Min(min, minVal[i]);
                     max = Math.Max(max, maxVal[i]);
                  }
                  double scale = 1.0, shift = 0.0;
                  if (max > 255.0 || min < 0)
                  {
                     scale = (max == min) ? 0.0 : 255.0 / (max - min);
                     shift = (scale == 0) ? min : -min * scale;
                  }

                  OclInvoke.ConvertTo(srcImage.Ptr, Ptr, scale, shift);
               }
               else
               {
                  OclInvoke.ConvertTo(srcImage.Ptr, Ptr, 1.0, 0.0);
               }

            }
            #endregion
         }
         else
         {
            #region different color
            if (typeof(TDepth) == typeof(TSrcDepth))
            {   //same depth
               ConvertColor(srcImage.Ptr, Ptr, typeof(TSrcColor), typeof(TColor), Size);
            }
            else
            {   //different depth
               using (OclImage<TSrcColor, TDepth> tmp = srcImage.Convert<TSrcColor, TDepth>()) //convert depth
                  ConvertColor(tmp.Ptr, Ptr, typeof(TSrcColor), typeof(TColor), Size);
            }
            #endregion
         }
      }

      private static void ConvertColor(IntPtr src, IntPtr dest, Type srcColor, Type destColor, Size size)
      {
         try
         {
            // if the direct conversion exist, apply the conversion
            OclInvoke.CvtColor(src, dest, CvToolbox.GetColorCvtCode(srcColor, destColor));
         }
         catch
         {
            try
            {
               //if a direct conversion doesn't exist, apply a two step conversion
               //in this case, needs to wait for the completion of the stream because a temporary local image buffer is used
               //we don't want the tmp image to be released before the operation is completed.
               using (OclImage<Bgr, TDepth> tmp = new OclImage<Bgr, TDepth>(size))
               {
                  OclInvoke.CvtColor(src, tmp.Ptr, CvToolbox.GetColorCvtCode(srcColor, typeof(Bgr)));
                  OclInvoke.CvtColor(tmp.Ptr, dest, CvToolbox.GetColorCvtCode(typeof(Bgr), destColor));
               }
            }
            catch
            {
               throw new NotSupportedException(String.Format(
                  "Convertion from OclImage<{0}, {1}> to OclImage<{2}, {3}> is not supported by OpenCV",
                  srcColor.ToString(),
                  typeof(TDepth).ToString(),
                  destColor.ToString(),
                  typeof(TDepth).ToString()));
            }
         }
      }
   }
}