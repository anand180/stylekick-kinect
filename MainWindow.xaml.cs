/////////////////////////////////////////////////////////////////////////
//
// This module contains code to do Kinect NUI initialization and
// processing and also to display NUI streams on screen.
//
// Copyright © Microsoft Corporation.  All rights reserved.  
// This code is licensed under the terms of the 
// Microsoft Kinect for Windows SDK (Beta) from Microsoft Research 
// License Agreement: http://research.microsoft.com/KinectSDK-ToU
//
/////////////////////////////////////////////////////////////////////////
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Research.Kinect.Nui;

namespace SkeletalViewer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        Device kinects;
        Runtime[] nui = new Runtime[3];
        Runtime kinect1, kinect2, kinect3;
        int totalFrames = 0;
        int lastFrames = 0;

        int[,] transformation21 = new int[3, 3];

        DateTime lastTime = DateTime.MaxValue;

        bool depth1Ready = false, depth2Ready = false, color1Ready = false, color2Ready= false;
        PlanarImage depthImage1, depthImage2, colorImage1, colorImage2;
        ImageViewArea ViewArea;

        int[,] calibrationValues = new int[3,6];


        const int START = 0;
        const int CALIBRATE = 1;
        const int WAIT = 2;
        const int SCAN = 3;
        const int DONE = 4;

        static int state = START;

        // We want to control how depth data gets converted into false-color data
        // for more intuitive visualization, so we keep 32-bit color frame buffer versions of
        // these, to be updated whenever we receive and process a 16-bit frame.
        const int RED_IDX = 2;
        const int GREEN_IDX = 1;
        const int BLUE_IDX = 0;
        byte[] depthFrame32 = new byte[320 * 240 * 4];
        byte[] colorFrame = new byte[320 * 240 * 4];
        
        
        Dictionary<JointID,Brush> jointColors = new Dictionary<JointID,Brush>() { 
            {JointID.HipCenter, new SolidColorBrush(Color.FromRgb(169, 176, 155))},
            {JointID.Spine, new SolidColorBrush(Color.FromRgb(169, 176, 155))},
            {JointID.ShoulderCenter, new SolidColorBrush(Color.FromRgb(168, 230, 29))},
            {JointID.Head, new SolidColorBrush(Color.FromRgb(200, 0,   0))},
            {JointID.ShoulderLeft, new SolidColorBrush(Color.FromRgb(79,  84,  33))},
            {JointID.ElbowLeft, new SolidColorBrush(Color.FromRgb(84,  33,  42))},
            {JointID.WristLeft, new SolidColorBrush(Color.FromRgb(255, 126, 0))},
            {JointID.HandLeft, new SolidColorBrush(Color.FromRgb(215,  86, 0))},
            {JointID.ShoulderRight, new SolidColorBrush(Color.FromRgb(33,  79,  84))},
            {JointID.ElbowRight, new SolidColorBrush(Color.FromRgb(33,  33,  84))},
            {JointID.WristRight, new SolidColorBrush(Color.FromRgb(77,  109, 243))},
            {JointID.HandRight, new SolidColorBrush(Color.FromRgb(37,   69, 243))},
            {JointID.HipLeft, new SolidColorBrush(Color.FromRgb(77,  109, 243))},
            {JointID.KneeLeft, new SolidColorBrush(Color.FromRgb(69,  33,  84))},
            {JointID.AnkleLeft, new SolidColorBrush(Color.FromRgb(229, 170, 122))},
            {JointID.FootLeft, new SolidColorBrush(Color.FromRgb(255, 126, 0))},
            {JointID.HipRight, new SolidColorBrush(Color.FromRgb(181, 165, 213))},
            {JointID.KneeRight, new SolidColorBrush(Color.FromRgb(71, 222,  76))},
            {JointID.AnkleRight, new SolidColorBrush(Color.FromRgb(245, 228, 156))},
            {JointID.FootRight, new SolidColorBrush(Color.FromRgb(77,  109, 243))}
        };

        private void Window_Loaded(object sender, EventArgs e)
        {
            
            kinects = new Device();
            int count = kinects.Count;
            System.Console.Out.WriteLine("Devices created");
            
            //System.Windows.MessageBox.Show("Only " + count + " kinects available");


            //for (int i = 0; i < 3; i++)
            //    nui[i] = new Runtime(i);

            kinect1 = new Runtime(0);
            
            kinect2 = new Runtime(1);
            //kinect3 = new Runtime(2);

            try
            {
                kinect1.Initialize(RuntimeOptions.UseDepth | RuntimeOptions.UseColor);
                kinect1.VideoStream.Open(ImageStreamType.Video, 2, ImageResolution.Resolution640x480, ImageType.Color);
                kinect1.DepthStream.Open(ImageStreamType.Depth, 2, ImageResolution.Resolution320x240, ImageType.Depth);
                kinect1.DepthFrameReady += new EventHandler<ImageFrameReadyEventArgs>(nui_DepthFrameReady);
                //kinect1.SkeletonFrameReady += new EventHandler<SkeletonFrameReadyEventArgs>(nui_SkeletonFrameReady);
                kinect1.VideoFrameReady += new EventHandler<ImageFrameReadyEventArgs>(nui_ColorFrameReady);

                //System.Windows.MessageBox.Show("Initialization of Kinect " + kinect1.InstanceIndex + " Done");
            }
            catch (InvalidOperationException)
            {
                System.Windows.MessageBox.Show("Runtime initialization " + kinect1.InstanceIndex + " failed. Please make sure Kinect device is plugged in.");
                return;
            }
            //try
            //{
            //    kinect3.Initialize(RuntimeOptions.UseDepth | RuntimeOptions.UseColor);
            //    kinect3.VideoStream.Open(ImageStreamType.Video, 2, ImageResolution.Resolution640x480, ImageType.Color);
            //    kinect3.DepthStream.Open(ImageStreamType.Depth, 2, ImageResolution.Resolution320x240, ImageType.Depth);
            //    kinect3.DepthFrameReady += new EventHandler<ImageFrameReadyEventArgs>(nui_DepthFrameReady3);
            //    kinect3.VideoFrameReady += new EventHandler<ImageFrameReadyEventArgs>(nui_ColorFrameReady3);
            //    System.Windows.MessageBox.Show("Initialization of Kinect " + kinect3.InstanceIndex + " Done");
            //}
            //    catch (InvalidOperationException)
            //{
            //    System.Windows.MessageBox.Show("Runtime initialization " + kinect3.InstanceIndex + " failed. Please make sure Kinect device is plugged in.");
            //    return;
            //}
            try
            {
                kinect2.Initialize(RuntimeOptions.UseDepth | RuntimeOptions.UseColor);
                kinect2.VideoStream.Open(ImageStreamType.Video, 2, ImageResolution.Resolution640x480, ImageType.Color);
                kinect2.DepthStream.Open(ImageStreamType.Depth, 2, ImageResolution.Resolution320x240, ImageType.Depth);
                kinect2.DepthFrameReady += new EventHandler<ImageFrameReadyEventArgs>(nui_DepthFrameReady2);
                kinect2.VideoFrameReady += new EventHandler<ImageFrameReadyEventArgs>(nui_ColorFrameReady2);

                

                //nui[0].Initialize(RuntimeOptions.UseDepthAndPlayerIndex | RuntimeOptions.UseSkeletalTracking | RuntimeOptions.UseColor);
                //nui[1].Initialize(RuntimeOptions.UseDepth | RuntimeOptions.UseColor);
                //nui[2].Initialize(RuntimeOptions.UseDepth | RuntimeOptions.UseColor);
            }
            catch (InvalidOperationException)
            {
                System.Windows.MessageBox.Show("Runtime initialization " + kinect2.InstanceIndex + " failed. Please make sure Kinect device is plugged in.");
                return;
            }

            //System.Windows.MessageBox.Show("Initialization Done");

            //Camera sensor = nui[0].NuiCamera;
            //string name = sensor.UniqueDeviceName;
            //System.Windows.MessageBox.Show(name);
            try
            {
                
                
                //nui[2].VideoStream.Open(ImageStreamType.Video, 2, ImageResolution.Resolution640x480, ImageType.Color);
                //nui[2].DepthStream.Open(ImageStreamType.Depth, 2, ImageResolution.Resolution320x240, ImageType.Depth);
            }
            catch (InvalidOperationException)
            {
                System.Windows.MessageBox.Show("Failed to open stream. Please make sure to specify a supported image type and resolution.");
                return;
            }

            //System.Windows.MessageBox.Show("Streams Open");

            lastTime = DateTime.Now;


            if (System.Windows.Forms.MessageBox.Show("Begin the Calibration Process", "Calibration", System.Windows.Forms.MessageBoxButtons.YesNo) == System.Windows.Forms.DialogResult.Yes)
            {
                //do something in your code that deals with the situation if the user confirms (presses the yes button)

            }
            else
            {
                //do something with the code that deals with the situation that user presses the no button
                return;
            }

            //wait for calibration;
            while (calibrate() != 0)
            {
            }



            //nui[2].DepthFrameReady += new EventHandler<ImageFrameReadyEventArgs>(nui_DepthFrameReady3);
            //nui[2].VideoFrameReady += new EventHandler<ImageFrameReadyEventArgs>(nui_ColorFrameReady3);
        }


        int calibrate()
        {
            
            //check if we received the first frame from each sensor
            if (depth1Ready && depth2Ready && color1Ready && color2Ready)
            {
                int x1, y1, z1, x2, y2, z2;
                findCalibrator(kinect1, depthImage1, colorImage1, out calibrationValues[0,0], out calibrationValues[0,1], out calibrationValues[0,2], out calibrationValues[0,3], out calibrationValues[0,4], out calibrationValues[0,5]);
                findCalibrator(kinect2, depthImage2, colorImage2, out calibrationValues[1, 0], out calibrationValues[1, 1], out calibrationValues[1, 2], out calibrationValues[1, 3], out calibrationValues[1, 4], out calibrationValues[1, 5]);

                int[] a = new int[6];
                int[] b = new int[6];
                convert2Dto1D(calibrationValues, 0, 6, out a);
                convert2Dto1D(calibrationValues, 1, 6, out b);
                //createTransformationMatrix(a, b);
            }
            else
                return 1;
            return 0;
        }

        private void createTransformationMatrix(int[] a, int[] b)
        {

        }

        private void convert2Dto1D(int[,] a, int i, int length, out int[] b)
        {
            b = new int[6];
            for (int j = 0; j < length; j++)
            {
                b[j] = a[i, j];
            }

        }

        int findCalibrator(Runtime kinect, PlanarImage depth, PlanarImage color, out int x1, out int y1, out int z1, out int x2, out int y2, out int z2)
        {
            x1 = -1;
            y1 = -1; 
            z1 = -1;
            x2 = -1;
            y2 = -1;
            z2 = -1;

            //check from 1500mm to 2500mm for the calibrator
            const int minDepth = 1500;
            const int maxDepth = 2500;

            const int BLUEMIN = 50;
            const int REDMAX = 50;
            const int GREENMAX = 50;
            int[] start = { -1, -1, -1 };
            int[] end = { -1, -1, -1 };

            int height = depth.Height;
            int width = depth.Width;
            int i = 0;

            bool foundCalibrator = false;
            bool endloop = false;

            //traverse left to right and up to down to find the top centre of the calibrator
            for (int y = 0; y < height && !endloop; y++)
            {
                int heightOffset = y*width;

                for (int x = 0; x < width && !endloop; x++)
                {
                    int z = getDistance(depth.Bits[i], depth.Bits[i+1]);
                    if ( z < maxDepth && z > minDepth){
                        //check for the colour
                        int red, green, blue;
                        short distance = (short)(z << 3);

                        getColorFromDepth(kinect, depth, color, x, y, z, out red, out green, out blue);

                        
                        if (!foundCalibrator) //check to see if we found the blue cylinder
                        {

                            if (blue > BLUEMIN)
                            {
                                System.Windows.MessageBox.Show("1-1 Kinect " + kinect.InstanceIndex + " - R:" + red + " G:" + green + " B:" + blue + " at x:" + x + " y:" + y + " z:" + z);

                                foundCalibrator = true;
                                start[0] = x;
                                start[1] = y;
                                start[2] = z;

                            }
                        }
                        else //check to see if we passed the blue cylinder
                        {
                            if (blue < BLUEMIN)
                            {
                                System.Windows.MessageBox.Show("1-2 Kinect " + kinect.InstanceIndex + " - R:" + red + " G:" + green + " B:" + blue + " at x:" + x + " y:" + y + " z:" + z);

                                end[0] = x;
                                end[1] = y;
                                end[2] = z;

                                getMidpoint(start, end, out x1, out y1, out z1);
                                endloop = true;
                            }
                        }

                    }

                    i +=2;
                }
            }

            //start from the end of the image and work backwards
            foundCalibrator = false;
            i = depth.Bits.Length- 2;

            

            for (int y = width-1; y >= 0 && i>0; y--)
            {
                int heightOffset = y * width;

                for (int x = width -1; x >= 0; x--)
                {
                    int z = getDistance(depth.Bits[i], depth.Bits[i + 1]);
                    if (z < maxDepth && z > minDepth)
                    {
                        //System.Windows.MessageBox.Show("something found!");

                        //check for the colour
                        int red, green, blue;
                        short distance = (short)(z << 3);

                        getColorFromDepth(kinect, depth, color, x, y, z, out red, out green, out blue);

                        if (!foundCalibrator) //check to see if we found the blue cylinder
                        {
                            //System.Windows.MessageBox.Show("R:" + red + " G:" + green + " B:" + blue);

                            if (blue > BLUEMIN && red < REDMAX & green < GREENMAX)
                            {
                                System.Windows.MessageBox.Show("2-1 Kinect " + kinect.InstanceIndex + " - R:" + red + " G:" + green + " B:" + blue + " at x:" + x + " y:" + y + " z:" + z);

                                foundCalibrator = true;
                                start[0] = x;
                                start[1] = y;
                                start[2] = z;

                            }
                        }
                        else //check to see if we passed the blue cylinder
                        {
                            if (blue < BLUEMIN && red > REDMAX & green > GREENMAX)
                            {
                                System.Windows.MessageBox.Show("2-2 Kinect " + kinect.InstanceIndex + " - R:" + red + " G:" + green + " B:" + blue + " at x:" + x + " y:" + y + " z:" + z);

                                end[0] = x;
                                end[1] = y;
                                end[2] = z;

                                getMidpoint(start, end, out x2, out y2, out z2);
                                return 0;
                            }
                        }

                    }

                    i -= 2;
                }
            }


            return -1;
        }

        private void getMidpoint(int[] a, int[] b, out int x, out int y, out int z)
        {
            x = (a[0] + b[0]) / 2;
            y = (a[1] + b[1]) / 2;
            z = (a[2] + b[2]) / 2;
        }

        private void getColorFromDepth(Runtime kinect, PlanarImage depth, PlanarImage color, int x, int y, int z, out int red,  out int green, out int blue)
        {
            short distance = (short)(z << 3);
            int colorX, colorY;

            kinect.NuiCamera.GetColorPixelCoordinatesFromDepthPixel(ImageResolution.Resolution640x480, ViewArea, x, y, distance, out colorX, out colorY);
            getColor(color, colorX, colorY, out red, out green, out blue);


        }


        private void getColor(PlanarImage color, int x, int y, out int red, out int green, out int blue)
        {
            int i = x + y*color.Height;
            red = color.Bits[i + RED_IDX];
            green = color.Bits[i + GREEN_IDX];
            blue = color.Bits[i + BLUE_IDX];
        }

        private int getDistance(byte firstFrame, byte secondFrame)
        {
            int distance = (int)(firstFrame | secondFrame << 8);
            return distance;
        }


        // Converts a 16-bit grayscale depth frame which includes player indexes into a 32-bit frame
        // that displays different players in different colors
        byte[] convertDepthFrame(byte[] depthFrame16)
        {
            for (int i16 = 0, i32 = 0; i16 < depthFrame16.Length && i32 < depthFrame32.Length; i16 += 2, i32 += 4)
            {
                int player = depthFrame16[i16] & 0x07;
                int realDepth = (depthFrame16[i16+1] << 5) | (depthFrame16[i16] >> 3);
                // transform 13-bit depth information into an 8-bit intensity appropriate
                // for display (we disregard information in most significant bit)
                byte intensity = (byte)(255 - (255 * realDepth / 0x0fff));

                depthFrame32[i32 + RED_IDX] = 0;
                depthFrame32[i32 + GREEN_IDX] = 0;
                depthFrame32[i32 + BLUE_IDX] = 0;

                // choose different display colors based on player
                switch (player)
                {
                    case 0:
                        depthFrame32[i32 + RED_IDX] = (byte)(intensity / 2);
                        depthFrame32[i32 + GREEN_IDX] = (byte)(intensity / 2);
                        depthFrame32[i32 + BLUE_IDX] = (byte)(intensity / 2);
                        break;
                    case 1:
                        depthFrame32[i32 + RED_IDX] = intensity;
                        break;
                    case 2:
                        depthFrame32[i32 + GREEN_IDX] = intensity;
                        break;
                    case 3:
                        depthFrame32[i32 + RED_IDX] = (byte)(intensity / 4);
                        depthFrame32[i32 + GREEN_IDX] = (byte)(intensity);
                        depthFrame32[i32 + BLUE_IDX] = (byte)(intensity);
                        break;
                    case 4:
                        depthFrame32[i32 + RED_IDX] = (byte)(intensity);
                        depthFrame32[i32 + GREEN_IDX] = (byte)(intensity);
                        depthFrame32[i32 + BLUE_IDX] = (byte)(intensity / 4);
                        break;
                    case 5:
                        depthFrame32[i32 + RED_IDX] = (byte)(intensity);
                        depthFrame32[i32 + GREEN_IDX] = (byte)(intensity / 4);
                        depthFrame32[i32 + BLUE_IDX] = (byte)(intensity);
                        break;
                    case 6:
                        depthFrame32[i32 + RED_IDX] = (byte)(intensity / 2);
                        depthFrame32[i32 + GREEN_IDX] = (byte)(intensity / 2);
                        depthFrame32[i32 + BLUE_IDX] = (byte)(intensity);
                        break;
                    case 7:
                        depthFrame32[i32 + RED_IDX] = (byte)(255 - intensity);
                        depthFrame32[i32 + GREEN_IDX] = (byte)(255 - intensity);
                        depthFrame32[i32 + BLUE_IDX] = (byte)(255 - intensity);
                        break;
                }

                if (colorFrame != null && (i16 % 500 == 0))
                {
                    ImageViewArea iv = new ImageViewArea();

                    

                    int depthX = i16 % 320;
                    int depthY = i16 / 320;
                    int colorX, colorY;
                    //depthX = Math.Max(0, Math.Min(depthX * 320, 320));  //convert to 320, 240 space
                    //depthY = Math.Max(0, Math.Min(depthY * 240, 240));  //convert to 320, 240 space

                    kinect1.NuiCamera.GetColorPixelCoordinatesFromDepthPixel(ImageResolution.Resolution640x480, iv, depthX, depthY, (short)realDepth, out colorX, out colorY);

                    int index = colorX + colorY * 320;
                    //System.Console.WriteLine(depthX + "," + depthY);
                    //depthFrame32[i32 + RED_IDX] = colorFrame[index + RED_IDX];
                    //depthFrame32[i32 + GREEN_IDX] = colorFrame[index + GREEN_IDX]; ;
                    //depthFrame32[i32 + BLUE_IDX] = colorFrame[index + BLUE_IDX]; ;

                }
            }
            return depthFrame32;
        }

        void nui_DepthFrameReady(object sender, ImageFrameReadyEventArgs e)
        {
            PlanarImage Image = e.ImageFrame.Image;
            ViewArea = e.ImageFrame.ViewArea;
            depthImage1 = Image;
            depth1Ready = true;
            byte[] convertedDepthFrame = convertDepthFrame(Image.Bits);

            
            depth.Source = BitmapSource.Create(
                Image.Width, Image.Height, 96, 96, PixelFormats.Bgr32, null, convertedDepthFrame, Image.Width * 4);

            ++totalFrames;

            DateTime cur = DateTime.Now;
            if (cur.Subtract(lastTime) > TimeSpan.FromSeconds(1))
            {
                int frameDiff = totalFrames - lastFrames;
                lastFrames = totalFrames;
                lastTime = cur;
                frameRate.Text = frameDiff.ToString() + " fps";
            }
        }

        void nui_DepthFrameReady2(object sender, ImageFrameReadyEventArgs e)
        {
            PlanarImage Image = e.ImageFrame.Image;

            depthImage2 = Image;
            depth2Ready = true;

            byte[] convertedDepthFrame = convertDepthFrame(Image.Bits);



            depth2.Source = BitmapSource.Create(
                Image.Width, Image.Height, 96, 96, PixelFormats.Bgr32, null, convertedDepthFrame, Image.Width * 4);

            //++totalFrames;

            //DateTime cur = DateTime.Now;
            //if (cur.Subtract(lastTime) > TimeSpan.FromSeconds(1))
            //{
            //    int frameDiff = totalFrames - lastFrames;
            //    lastFrames = totalFrames;
            //    lastTime = cur;
            //    frameRate.Text = frameDiff.ToString() + " fps";
            //}
        }

        void nui_DepthFrameReady3(object sender, ImageFrameReadyEventArgs e)
        {
            PlanarImage Image = e.ImageFrame.Image;
            byte[] convertedDepthFrame = convertDepthFrame(Image.Bits);


            depth3.Source = BitmapSource.Create(
                Image.Width, Image.Height, 96, 96, PixelFormats.Bgr32, null, convertedDepthFrame, Image.Width * 4);

            //++totalFrames;

            //DateTime cur = DateTime.Now;
            //if (cur.Subtract(lastTime) > TimeSpan.FromSeconds(1))
            //{
            //    int frameDiff = totalFrames - lastFrames;
            //    lastFrames = totalFrames;
            //    lastTime = cur;
            //    frameRate.Text = frameDiff.ToString() + " fps";
            //}
        }



        private Point getDisplayPosition(Joint joint)
        {
            float depthX, depthY;
            kinect1.SkeletonEngine.SkeletonToDepthImage(joint.Position, out depthX, out depthY);
            depthX = Math.Max(0, Math.Min(depthX * 320, 320));  //convert to 320, 240 space
            depthY = Math.Max(0, Math.Min(depthY * 240, 240));  //convert to 320, 240 space
            int colorX, colorY;
            ImageViewArea iv = new ImageViewArea();
            // only ImageResolution.Resolution640x480 is supported at this point
            kinect1.NuiCamera.GetColorPixelCoordinatesFromDepthPixel(ImageResolution.Resolution640x480, iv, (int)depthX, (int)depthY, (short)0, out colorX, out colorY);

            // map back to skeleton.Width & skeleton.Height
            return new Point((int)(skeleton.Width * colorX / 640.0), (int)(skeleton.Height * colorY / 480));
        }

        Polyline getBodySegment(Microsoft.Research.Kinect.Nui.JointsCollection joints, Brush brush, params JointID[] ids)
        {
            PointCollection points = new PointCollection(ids.Length);
            for (int i = 0; i < ids.Length; ++i )
            {
                points.Add(getDisplayPosition(joints[ids[i]]));
            }

            Polyline polyline = new Polyline();
            polyline.Points = points;
            polyline.Stroke = brush;
            polyline.StrokeThickness = 5;
            return polyline;
        }

        void nui_SkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            SkeletonFrame skeletonFrame = e.SkeletonFrame;
            int iSkeleton = 0;
            Brush[] brushes = new Brush[6];
            brushes[0] = new SolidColorBrush(Color.FromRgb(255, 0, 0));
            brushes[1] = new SolidColorBrush(Color.FromRgb(0, 255, 0));
            brushes[2] = new SolidColorBrush(Color.FromRgb(64, 255, 255));
            brushes[3] = new SolidColorBrush(Color.FromRgb(255, 255, 64));
            brushes[4] = new SolidColorBrush(Color.FromRgb(255, 64, 255));
            brushes[5] = new SolidColorBrush(Color.FromRgb(128, 128, 255));

            skeleton.Children.Clear();
            foreach (SkeletonData data in skeletonFrame.Skeletons)
            {
                if (SkeletonTrackingState.Tracked == data.TrackingState)
                {
                    // Draw bones
                    Brush brush = brushes[iSkeleton % brushes.Length];
                    skeleton.Children.Add(getBodySegment(data.Joints, brush, JointID.HipCenter, JointID.Spine, JointID.ShoulderCenter, JointID.Head));
                    skeleton.Children.Add(getBodySegment(data.Joints, brush, JointID.ShoulderCenter, JointID.ShoulderLeft, JointID.ElbowLeft, JointID.WristLeft, JointID.HandLeft));
                    skeleton.Children.Add(getBodySegment(data.Joints, brush, JointID.ShoulderCenter, JointID.ShoulderRight, JointID.ElbowRight, JointID.WristRight, JointID.HandRight));
                    skeleton.Children.Add(getBodySegment(data.Joints, brush, JointID.HipCenter, JointID.HipLeft, JointID.KneeLeft, JointID.AnkleLeft, JointID.FootLeft));
                    skeleton.Children.Add(getBodySegment(data.Joints, brush, JointID.HipCenter, JointID.HipRight, JointID.KneeRight, JointID.AnkleRight, JointID.FootRight));

                    // Draw joints
                    foreach (Joint joint in data.Joints)
                    {
                        Point jointPos = getDisplayPosition(joint);
                        Line jointLine = new Line();
                        jointLine.X1 = jointPos.X - 3;
                        jointLine.X2 = jointLine.X1 + 6;
                        jointLine.Y1 = jointLine.Y2 = jointPos.Y;
                        jointLine.Stroke = jointColors[joint.ID];
                        jointLine.StrokeThickness = 6;
                        skeleton.Children.Add(jointLine);
                    }
                }
                iSkeleton++;
            } // for each skeleton
        }

        void nui_ColorFrameReady(object sender, ImageFrameReadyEventArgs e)
        {
            // 32-bit per pixel, RGBA image
            PlanarImage Image = e.ImageFrame.Image;
            colorImage1 = Image;
            color1Ready = true;

            colorFrame = Image.Bits;

            video.Source = BitmapSource.Create(
                Image.Width, Image.Height, 96, 96, PixelFormats.Bgr32, null, Image.Bits, Image.Width * Image.BytesPerPixel);
        }
        void nui_ColorFrameReady2(object sender, ImageFrameReadyEventArgs e)
        {
            // 32-bit per pixel, RGBA image
            PlanarImage Image = e.ImageFrame.Image;
            colorImage2 = Image;
            color2Ready = true;

            video2.Source = BitmapSource.Create(
                Image.Width, Image.Height, 96, 96, PixelFormats.Bgr32, null, Image.Bits, Image.Width * Image.BytesPerPixel);
        }
        void nui_ColorFrameReady3(object sender, ImageFrameReadyEventArgs e)
        {
            // 32-bit per pixel, RGBA image
            PlanarImage Image = e.ImageFrame.Image;
            video3.Source = BitmapSource.Create(
                Image.Width, Image.Height, 96, 96, PixelFormats.Bgr32, null, Image.Bits, Image.Width * Image.BytesPerPixel);
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            kinect1.Uninitialize();
            Environment.Exit(0);
        }


    }
}
