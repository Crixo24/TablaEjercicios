/*  IMPORTANTE: Este proyecto está basado en el código de ejemplo KinectSkeleton que da Microsoft con el SDK de Kinect.
 */

namespace Microsoft.Samples.Kinect.SkeletonBasics
{
    using System;
    using System.IO;
    using System.Windows;
    using System.Windows.Media;
    using Microsoft.Kinect;
    using System.Windows.Shapes;
    using System.Windows.Controls;
    using System.Collections.Generic;
    

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        //Declaración de las variables de la clase

        private List<Skeleton> skeletonList = new List<Skeleton>();
        private int listSize = 1;
        private int frameInterval = 3;
        private int frameCount = 0;

        private const double HeadSize = 0.075;
        /// <summary>
        /// Width of output drawing
        /// </summary>
        private const float RenderWidth = 640.0f;///0.5f;

        /// <summary>
        /// Height of our output drawing
        /// </summary>
        private const float RenderHeight = 480.0f;///0.5f;

        /// <summary>
        /// Thickness of drawn joint lines
        /// </summary>
        private const double JointThickness = 3;
        private const double HeadThickness = 25;

        /// <summary>
        /// Thickness of body center ellipse
        /// </summary>
        private const double BodyCenterThickness = 10;

        /// <summary>
        /// Thickness of clip edge rectangles
        /// </summary>
        private const double ClipBoundsThickness = 10;

        /// <summary>
        /// Brush used to draw skeleton center point
        /// </summary>
        private readonly Brush centerPointBrush = Brushes.Blue;

        /// <summary>
        /// Brush used for drawing joints that are currently tracked
        /// </summary>
        private readonly Brush trackedJointBrush = new SolidColorBrush(Color.FromArgb(255, 10, 255, 255));

        private readonly Brush headBrush = new SolidColorBrush(Color.FromArgb(255, 255, 25, 25));

        /// <summary>
        /// Brush used for drawing joints that are currently inferred
        /// </summary>        
        private readonly Brush inferredJointBrush = Brushes.Yellow;

        /// <summary>
        /// Pen used for drawing bones that are currently tracked
        /// </summary>
        private Pen trackedBonePen = new Pen(Brushes.Blue, 6);
        private Pen graphPen = new Pen(Brushes.White, 2);

        /// <summary>
        /// Pen used for drawing bones that are currently inferred
        /// </summary>        
        private readonly Pen inferredBonePen = new Pen(Brushes.Gray, 1);

        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        private KinectSensor sensor;

        /// <summary>
        /// Drawing group for skeleton rendering output
        /// </summary>
        private DrawingGroup drawingGroup;

        /// <summary>
        /// Drawing image that we will display
        /// </summary>
        private DrawingImage imageSource;

        /// <summary>
        /// Variables globales para el control de los ejercicios
        /// </summary>
        private int conteo = 0;
        private int ejercicio = 0;
        private int repeticiones = 5;
        private bool actual, anterior = false;
        private int estado = -1;

        /// <summary>
        /// Initializes a new instance of the MainWindow class.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            textBox1.Text = "Ejercicio 1: subir ambos brazos por encima de la cabeza.";
        }

        /// <summary>
        /// Draws indicators to show which edges are clipping skeleton data
        /// </summary>
        /// <param name="skeleton">skeleton to draw clipping information for</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private static void RenderClippedEdges(Skeleton skeleton, DrawingContext drawingContext)
        {
            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Bottom))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, RenderHeight - ClipBoundsThickness, RenderWidth, ClipBoundsThickness));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Top))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, RenderWidth, ClipBoundsThickness));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Left))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, ClipBoundsThickness, RenderHeight));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Right))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(RenderWidth - ClipBoundsThickness, 0, ClipBoundsThickness, RenderHeight));
            }
        }

        /// <summary>
        /// Execute startup tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            // Create the drawing group we'll use for drawing
            this.drawingGroup = new DrawingGroup();

            // Create an image source that we can use in our image control
            this.imageSource = new DrawingImage(this.drawingGroup);

            // Display the drawing using our image control
            Image.Source = this.imageSource;

            // Look through all sensors and start the first connected one.
            // This requires that a Kinect is connected at the time of app startup.
            // To make your app robust against plug/unplug, 
            // it is recommended to use KinectSensorChooser provided in Microsoft.Kinect.Toolkit (See components in Toolkit Browser).
            foreach (var potentialSensor in KinectSensor.KinectSensors)
            {
                if (potentialSensor.Status == KinectStatus.Connected)
                {
                    this.sensor = potentialSensor;
                    break;
                }
            }

            if (null != this.sensor)
            {
                // Turn on the skeleton stream to receive skeleton frames
                this.sensor.SkeletonStream.Enable();

                // Add an event handler to be called whenever there is new color frame data
                this.sensor.SkeletonFrameReady += this.SensorSkeletonFrameReady;

                // Start the sensor!
                try
                {
                    this.sensor.Start();
                }
                catch (IOException)
                {
                    this.sensor = null;
                }
            }

            if (null == this.sensor)
            {
                this.statusBarText.Text = Properties.Resources.NoKinectReady;
            }
        }

        /// <summary>
        /// Execute shutdown tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (null != this.sensor)
            {
                this.sensor.Stop();
            }
        }

        /// <summary>
        /// Translates the skeletons position based on its location in the List
        /// </summary>
        /// <param name="joint">Joint of the skeleton being translated</param>
        /// <param name="pos">Position in the list</param>
        /// <returns>Returns a new point</returns>
        private SkeletonPoint TranslateSkeletonPosition(Joint joint, int pos)
        {
            float trans = 0.05f;

            float skeleX = joint.Position.X;
            float skeleY = joint.Position.Y;
            float skeleZ = joint.Position.Z;

            skeleX += trans*pos;
            skeleY += trans*pos;
            skeleZ += trans*pos;

            SkeletonPoint skelePoint = new SkeletonPoint()
            {
                X = (float)skeleX, 
                Y = (float)skeleY, 
                Z = (float)skeleZ
            };

            return skelePoint;
            //pos++;
        }

        /// <summary>
        /// Event handler for Kinect sensor's SkeletonFrameReady event
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void SensorSkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            Skeleton[] skeletons = new Skeleton[0];

            int aux = -1;

            using (SkeletonFrame skeletonFrame = e.OpenSkeletonFrame())
            {
                if (skeletonFrame != null)
                {
                    skeletons = new Skeleton[skeletonFrame.SkeletonArrayLength];
                    skeletonFrame.CopySkeletonDataTo(skeletons);
                }
            }

            using (DrawingContext dc = this.drawingGroup.Open())
            {
                // Draw a transparent background to set the render size
                dc.DrawRectangle(Brushes.Black, null, new Rect(0.0, 0.0, RenderWidth, RenderHeight));

                if (skeletons.Length != 0)
                {
                    foreach (Skeleton skel in skeletons)
                    {
                        // Restrict the list of skeletons to 10
                        if (skeletonList.Count > listSize)
                        {
                            skeletonList.RemoveAt(listSize - 1);
                        }// end if

                        // Only capture the skeleton every 2 frames
                        if (frameCount % frameInterval == 0)
                        {
                            skeletonList.Insert(0, skel);
                            frameCount = 0;
                        }// end if
                        frameCount++;

                        RenderClippedEdges(skel, dc);

                        if (skel.TrackingState == SkeletonTrackingState.Tracked) {
                            switch (ejercicio) {
                                case 0:
                                    // Ejercicio 1
                                    this.trackedBonePen = new Pen(Brushes.Blue, 6);
                                    anterior = actual;
                                    actual = Ejercicio_1(skel);
                                    if (anterior && !actual) {
                                        conteo++;
                                        this.trackedBonePen = new Pen(Brushes.Green, 6);
                                        textBox1.Text = "Ejercicio 1: subir ambos brazos por encima de la cabeza. \nRepeticiones hasta ahora: " + conteo.ToString() + " de " + repeticiones.ToString();
                                    }

                                    if (conteo >= repeticiones) {
                                        ejercicio++;
                                        conteo = 0;
                                    }
                                    break;
                                case 1:
                                    // Ejercicio 2
                                    this.trackedBonePen = new Pen(Brushes.Blue, 6);
                                    aux = Ejercicio_2(skel, estado);
                                    if (aux == 1) estado = 1;
                                    if (aux == 2) estado = 2;
                                    if (aux == 0) estado = 0;

                                    textBox1.Text = "Ejercicio 2: Sube la rodilla derecha y luego la izquierda. \nRepeticiones hasta ahora: " + conteo.ToString() + " de " + repeticiones.ToString();
                                    if(estado == 0){
                                        conteo++;
                                        this.trackedBonePen = new Pen(Brushes.Green, 6);
                                        estado = -1;
                                        aux = -1;
                                        if(conteo >= repeticiones){
                                            ejercicio++;
                                            conteo=0;
                                        }
                                    }

                                    break;
                                case 2:
                                    // Ejercicio 3
                                    this.trackedBonePen = new Pen(Brushes.Blue, 6);
                                    aux = Ejercicio_3(skel, estado);
                                    if (aux == 1) estado = 1;
                                    if (aux == 2) estado = 2;
                                    if (aux == 0) estado = 0;

                                    textBox1.Text = "Ejercicio 3: Da un puñetazo con la mano derecha y luego la izquierda. \nRepeticiones hasta ahora: " + conteo.ToString() + " de " + repeticiones.ToString();
                                    if(estado == 0){
                                        this.trackedBonePen = new Pen(Brushes.Green, 6);
                                        conteo++;
                                        estado = -1;
                                        aux = -1;
                                        if(conteo >= repeticiones){
                                            ejercicio++;
                                            conteo=0;
                                        }
                                    }
                                    break;

                                case 3:
                                    // Ejercicio 4
                                    this.trackedBonePen = new Pen(Brushes.Blue, 6);
                                    anterior = actual;
                                    textBox1.Text = "Ejercicio 4: Mariposa, abre los brazos hasta poner los puños por detrás del pecho. \nRepeticiones hasta ahora: " + conteo.ToString() + " de " + repeticiones.ToString();
                                    actual = Ejercicio_4(skel);
                                    if (anterior && !actual) {
                                        this.trackedBonePen = new Pen(Brushes.Green, 6);
                                        conteo++;
                                    }

                                    if (conteo >= repeticiones) {
                                        ejercicio++;
                                        conteo = 0;
                                    }
           
                                    break;

                                case 4:
                                    this.trackedBonePen = new Pen(Brushes.DeepPink, 6);
                                    textBox1.Text = "¡Has terminado!";
                                    break;
                                default:
                                    textBox1.Text = "Error."; 
                                    break;
                            }
                            

                            this.DrawBonesAndJoints(skeletonList, dc);
                        }
                        else if (skel.TrackingState == SkeletonTrackingState.PositionOnly)
                        {
                            dc.DrawEllipse(
                            this.centerPointBrush,
                            null,
                            this.SkeletonPointToScreen(skel.Position),
                            BodyCenterThickness,
                            BodyCenterThickness);
                            this.DrawBonesAndJoints(skeletonList, dc);
                        }
                    }
                }

                // prevent drawing outside of our render area
                this.drawingGroup.ClipGeometry = new RectangleGeometry(new Rect(0.0, 0.0, RenderWidth, RenderHeight));
            }
        }

        /// <summary>
        /// Draws a skeleton's bones and joints
        /// </summary>
        /// <param name="skeleton">skeleton to draw</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private void DrawBonesAndJoints(List<Skeleton> skel, DrawingContext drawingContext)
        {
            //drawingContext.DrawLine(graphPen, new Point(340.0, 420.0), new Point(560.0, 200.0));
            
            for (int i = listSize - 1; i >= 0; i--)
            {
                Skeleton skeleton = skel[i];
                // Render Torso
                this.DrawBone(skeleton, drawingContext, JointType.Head, JointType.ShoulderCenter, i);
                this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.ShoulderLeft, i);
                this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.ShoulderRight, i);
                this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.Spine, i);
                this.DrawBone(skeleton, drawingContext, JointType.Spine, JointType.HipCenter, i);
                this.DrawBone(skeleton, drawingContext, JointType.HipCenter, JointType.HipLeft, i);
                this.DrawBone(skeleton, drawingContext, JointType.HipCenter, JointType.HipRight, i);

                // Left Arm
                this.DrawBone(skeleton, drawingContext, JointType.ShoulderLeft, JointType.ElbowLeft, i);
                this.DrawBone(skeleton, drawingContext, JointType.ElbowLeft, JointType.WristLeft, i);
                this.DrawBone(skeleton, drawingContext, JointType.WristLeft, JointType.HandLeft, i);

                // Right Arm
                this.DrawBone(skeleton, drawingContext, JointType.ShoulderRight, JointType.ElbowRight, i);
                this.DrawBone(skeleton, drawingContext, JointType.ElbowRight, JointType.WristRight, i);
                this.DrawBone(skeleton, drawingContext, JointType.WristRight, JointType.HandRight, i);

                // Left Leg
                this.DrawBone(skeleton, drawingContext, JointType.HipLeft, JointType.KneeLeft, i);
                this.DrawBone(skeleton, drawingContext, JointType.KneeLeft, JointType.AnkleLeft, i);
                this.DrawBone(skeleton, drawingContext, JointType.AnkleLeft, JointType.FootLeft, i);

                // Right Leg
                this.DrawBone(skeleton, drawingContext, JointType.HipRight, JointType.KneeRight, i);
                this.DrawBone(skeleton, drawingContext, JointType.KneeRight, JointType.AnkleRight, i);
                this.DrawBone(skeleton, drawingContext, JointType.AnkleRight, JointType.FootRight, i);
                
                // Render Joints
                foreach (Joint joint in skeleton.Joints)
                {
                    Brush drawBrush = null;

                    if (joint.TrackingState == JointTrackingState.Tracked)
                    {
                        drawBrush = this.trackedJointBrush;
                    }
                    else if (joint.TrackingState == JointTrackingState.Inferred)
                    {
                        drawBrush = this.inferredJointBrush;
                    }

                    if (drawBrush != null)
                    {
                        // If joint type is Head, then draw a big circle
                        if (joint.JointType == JointType.Head)
                        {
                            //drawingContext.DrawEllipse(headBrush, null, this.SkeletonPointToScreen(joint.Position), HeadThickness, HeadThickness);
                            drawingContext.DrawEllipse(headBrush, null, this.SkeletonPointToScreen(TranslateSkeletonPosition(joint, i)), HeadThickness, HeadThickness);
                        }
                        else
                        {
                            //drawingContext.DrawEllipse(drawBrush, null, this.SkeletonPointToScreen(joint.Position), JointThickness, JointThickness);
                            drawingContext.DrawEllipse(drawBrush, null, this.SkeletonPointToScreen(TranslateSkeletonPosition(joint, i)), JointThickness, JointThickness);
                        }

                        if (joint.JointType == JointType.HipCenter)
                        {
                            //Console.Write("X: " + joint.Position.X + "\nY: " + joint.Position.Y + "\n");
                        }// end if
                    }// end if
                }// end foreach

                
            }
        }

        /// <summary>
        /// Maps a SkeletonPoint to lie within our render space and converts to Point
        /// </summary>
        /// <param name="skelpoint">point to map</param>
        /// <returns>mapped point</returns>
        private Point SkeletonPointToScreen(SkeletonPoint skelpoint)
        {
            // Convert point to depth space.  
            // We are not using depth directly, but we do want the points in our 640x480 output resolution.
            DepthImagePoint depthPoint = this.sensor.CoordinateMapper.MapSkeletonPointToDepthPoint(skelpoint, DepthImageFormat.Resolution640x480Fps30);
            return new Point(depthPoint.X, depthPoint.Y);
        }

        /// <summary>
        /// Draws a bone line between two joints
        /// </summary>
        /// <param name="skeleton">skeleton to draw bones from</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        /// <param name="jointType0">joint to start drawing from</param>
        /// <param name="jointType1">joint to end drawing at</param>
        private void DrawBone(Skeleton skeleton, DrawingContext drawingContext, JointType jointType0, JointType jointType1, int pos)
        {
            Joint joint0 = skeleton.Joints[jointType0];
            Joint joint1 = skeleton.Joints[jointType1];

            // If we can't find either of these joints, exit
            if (joint0.TrackingState == JointTrackingState.NotTracked ||
                joint1.TrackingState == JointTrackingState.NotTracked)
            {
                return;
            }

            // Don't draw if both points are inferred
            if (joint0.TrackingState == JointTrackingState.Inferred &&
                joint1.TrackingState == JointTrackingState.Inferred)
            {
                return;
            }

            // We assume all drawn bones are inferred unless BOTH joints are tracked
            Pen drawPen = this.inferredBonePen;
            if (joint0.TrackingState == JointTrackingState.Tracked && joint1.TrackingState == JointTrackingState.Tracked)
            {
                drawPen = this.trackedBonePen;
            }

            //drawingContext.DrawLine(drawPen, this.SkeletonPointToScreen(joint0.Position), this.SkeletonPointToScreen(joint1.Position));
            drawingContext.DrawLine(drawPen, this.SkeletonPointToScreen(this.TranslateSkeletonPosition(joint0, pos)), this.SkeletonPointToScreen(this.TranslateSkeletonPosition(joint1, pos)));
        }

        /// <summary>
        /// Handles the checking or unchecking of the seated mode combo box
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void CheckBoxSeatedModeChanged(object sender, RoutedEventArgs e)
        {
            if (null != this.sensor)
            {
                if (this.checkBoxSeatedMode.IsChecked.GetValueOrDefault())
                {
                    this.sensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Seated;
                }
                else
                {
                    this.sensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Default;
                }
            }
        }


        //EJERCICIOS
        private bool Ejercicio_1(Skeleton s)
        {
            // Levantar la mano derecha por encima de la derecha y más a la izquierda que el hombro izquierdo

            //Sacamos los datos (puntos de las articulaciones) del skeleton cuyo tracking está haciendo
            Joint rightHand = s.Joints[JointType.HandRight];        //Obtenemos la mano derecha
            Joint head = s.Joints[JointType.Head];                  //Obtenemos la cabeza
            Joint leftHand = s.Joints[JointType.HandLeft];    //Obtenemos el punto la mano izquierdo

            //Aquí, obtenemos la coordenada Y de cada uno de los puntos (articulaciones) obtenidas.
            double rightHandY = rightHand.Position.Y;
            double headY = head.Position.Y;
            double leftHandY = leftHand.Position.Y;

            return rightHandY > headY && leftHandY > headY;
        }

        private bool Ejer_Pu_Der(Skeleton s){
            Joint rightHand = s.Joints[JointType.HandRight];
            Joint ShoulderCenter = s.Joints[JointType.ShoulderCenter];

            double rightHandZ = rightHand.Position.Z;
            double ShoulderCenterZ = ShoulderCenter.Position.Z;

            return (rightHandZ < ShoulderCenterZ * 0.7);
        }

        private bool Ejer_Pu_Izq(Skeleton s)
        {
            Joint leftHand = s.Joints[JointType.HandLeft];
            Joint ShoulderCenter = s.Joints[JointType.ShoulderCenter];

            double leftHandZ = leftHand.Position.Z;
            double ShoulderCenterZ = ShoulderCenter.Position.Z;

            return (leftHandZ < ShoulderCenterZ * 0.7);
        }

        private int Ejercicio_2(Skeleton s, int estado){

            double rightHipY = s.Joints[JointType.HipRight].Position.Y;
            double leftHipY = s.Joints[JointType.HipLeft].Position.Y;

            double rightKneeY = s.Joints[JointType.KneeRight].Position.Y;
            double leftKneeY = s.Joints[JointType.KneeLeft].Position.Y;
            
            // Consideramos que se ha levantado la pierna cuando la rodilla está
            // al 60% de la altura de la cadera. 

            rightHipY = rightHipY*0.6;
            leftHipY = leftHipY*0.6;
            
            switch (estado){
                case -1:
                case 0:        // Hay que levantar la pierna derecha
                    if(rightKneeY >= rightHipY)
                        return 1;
                    else
                        return -1;
                case 1:         // Hay que levantar la pierna izquierda
                    if(leftKneeY >= leftHipY)
                        return 2;
                    else
                        return 1;
                case 2:         // Volver al reposo
                    
                    if(leftKneeY < leftHipY && rightKneeY < rightHipY)
                        return 0;
                    else
                        return 2;
                default:
                    return -1;
            }


        }

        private int Ejercicio_3(Skeleton s, int estado)
        {
            switch (estado)
            {
                case -1:
                case 0:        // Hay que dar un puñetazo con la derecha
                    if (Ejer_Pu_Der(s))
                        return 1;
                    else
                        return -1;
                case 1:         // Hay que dar un puñetazo con la izquierda
                    if (Ejer_Pu_Izq(s))
                        return 2;
                    else
                        return 1;
                case 2:         // Volver al reposo

                    if (!Ejer_Pu_Der(s) && !Ejer_Pu_Izq(s))
                        return 0;
                    else
                        return 2;
                default:
                    return -1;

            }


        }

        private bool Ejercicio_4(Skeleton s){
            Joint rightHand = s.Joints[JointType.HandRight];
            Joint leftHand = s.Joints[JointType.HandLeft];
            Joint ShoulderCenter = s.Joints[JointType.ShoulderCenter];

            double rightHandZ = rightHand.Position.Z;
            double leftHandZ = leftHand.Position.Z;
            double shoulderCenterZ = ShoulderCenter.Position.Z;

            return (rightHandZ > shoulderCenterZ) && (leftHandZ > shoulderCenterZ);
        }

    }
}