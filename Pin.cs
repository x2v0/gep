using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using DirectShowLib;
using System.Runtime.InteropServices;

namespace gep
{

    class Pin : Animated
    {
        PinDirection direction;
        public PinDirection Direction { get { return direction; } }
        
        Rectangle rect;
        public Rectangle Rect { 
            get { return rect; }
            set { rect = value; }
        }
        //public Point Location { set { rect.Location = value; } }

        Filter filter;
        public Filter Filter { get { return filter; } }

        PinConnection connection;
        public PinConnection Connection { get { return connection; } }

        string uniqname;
        public string UniqName { get { return uniqname; } }

        string name;
        public string Name { get { return name; } }

        IPin ipin;
        public IPin IPin { get { return ipin; } }

        public static Font pinfont = new Font("Arial", 8);
        static Brush pinbrush = new SolidBrush(Color.Cyan);

        int num;
        public int Num { get { return num; } }

        public Pin(PinDirection dir, Filter f, IPin _ipin, int _num) //x,y in pixels inside filter rect
        {
            direction = dir;
            filter = f;
            num = _num;
            rect = new Rectangle(0, 0, 10, 10);
            ipin = _ipin;
            PinInfo pinfo;
            ipin.QueryPinInfo(out pinfo);
            name = pinfo.name;
            uniqname = f.Name + "." + pinfo.name;
            DsUtils.FreePinInfo(pinfo);
        }

        Rectangle last_drawn_rect;

        public void Draw(Graphics g, int x, int y)
        {
            Rectangle rc = rect;
            rc.X += x;
            rc.Y += y;
            int delta = animation_state / 2;
            using (var br = new LinearGradientBrush(new Point(0, rc.Top), new Point(0, rc.Bottom),
                Color.FromArgb(200 + delta, 200 + delta, 200),
                Color.FromArgb(100 + delta, 100 + delta, 100)))
            {
                g.FillRectangle(br, rc);
                last_drawn_rect = rc;
                if (direction == PinDirection.Input)
                    g.DrawString(name, pinfont, pinbrush, rc.Right + 1, rc.Top);
                else
                {
                    SizeF sz = g.MeasureString(name, pinfont);
                    g.DrawString(name, pinfont, pinbrush, rc.Left - 1 - sz.Width, rc.Top);
                }
            }
        }

        public void CompleteConnect(PinConnection con, bool really)
        {
            connection = con;
            if (con == null && really)
            {
                Filter.Graph.Disconnect(ipin);                
            }
        }

        public List<InterfaceInfo> ScanInterfaces()
        {
            return FilterGraphTools.ScanInterfaces(ipin);
        }

        public bool HasPropertyPage()
        {
            return (ipin as ISpecifyPropertyPages) != null;
        }

        public void ShowPropertyPage(IntPtr parent)
        {
            if (ipin == null)
                throw new ArgumentNullException("ipin");

            if (HasPropertyPage())
            {
                DsCAUUID caGuid;
                int hr = (ipin as ISpecifyPropertyPages).GetPages(out caGuid);
                DsError.ThrowExceptionForHR(hr);

                try
                {
                    object[] objs = new object[1];
                    objs[0] = ipin;

                    NativeMethods.OleCreatePropertyFrame(
                        parent, 0, 0,
                        UniqName,
                        objs.Length, objs,
                        caGuid.cElems, caGuid.pElems,
                        0, 0,
                        IntPtr.Zero
                        );
                }
                finally
                {
                    Marshal.FreeCoTaskMem(caGuid.pElems);
                }
            }
        }

        public void GetStreamCaps()
        {
            IAMStreamConfig isc = IPin as IAMStreamConfig;
            if (isc == null) return;
            int count, size;
            int hr = isc.GetNumberOfCapabilities(out count, out size);
            DsError.ThrowExceptionForHR(hr);
            if (size == Marshal.SizeOf(typeof(VideoStreamConfigCaps)))
                ShowStreamCaps<VideoStreamConfigCaps>(count, size, isc);
            else
                ShowStreamCaps<AudioStreamConfigCaps>(count, size, isc);
        }

        static void ShowStreamCaps<T>(int count, int size, IAMStreamConfig isc)
        {
            IntPtr scc = Marshal.AllocHGlobal(size);
            List<StreamCaps<T>> list = new List<StreamCaps<T>>();
            for (int i = 0; i < count; i++)
            {
                AMMediaType mt;
                int hr = isc.GetStreamCaps(i, out mt, scc);
                DsError.ThrowExceptionForHR(hr);
                object vcaps = Marshal.PtrToStructure(scc, typeof(T));
                list.Add(new StreamCaps<T>(mt, (T)vcaps));
            }
            Program.mainform.propform.SetObject(list.ToArray());
            Marshal.FreeHGlobal(scc);
        }

        public string GetPinCategory()
        {
            if (ipin == null) return null;
            IKsPropertySet pset = ipin as IKsPropertySet;
            if (pset == null) return null;
            IntPtr pGuid = Marshal.AllocHGlobal(16);
            int ret = 0;
            pset.Get(PropSetID.Pin, 0, IntPtr.Zero, 0, pGuid, 16, out ret);
            Guid guid = (Guid) Marshal.PtrToStructure(pGuid, typeof(Guid));
            Marshal.FreeHGlobal(pGuid);

            foreach (System.Reflection.FieldInfo m in typeof(PinCategory).GetFields())
                if ((Guid)m.GetValue(null) == guid)
                    return m.Name;

            return null;
        }

        protected override bool IsSelected()
        {
            return connection != null;
        }

        protected override void Redraw()
        {
            filter.Graph.Form.Invalidate(last_drawn_rect);
        }

        protected override bool IsHovered()
        {
            return filter.PinInPoint(filter.Graph.Form.MousePos) == this;
        }

    }// end of Pin class

    struct ConStep
    {
        public int x, y, dir;
    }

    class PinConnection : Animated 
    {
        public Pin[] pins; //outpin, inpin
        public List<ConStep> path;
        int id;
        public int ID { get { return id; } }
        string uniqname;
        public string UniqName { get { return uniqname; } }
        static Pen beigePen = new Pen(Brushes.Green);
        static Pen yellowPen = new Pen(Brushes.Yellow);
        Rectangle last_drawn_rectangle;

        static int lastid = 1;
        
        public PinConnection(Pin outpin, Pin inpin)
        {
            id = lastid++;
            pins = new Pin[2] { outpin, inpin };
            inpin.CompleteConnect(this, true);
            outpin.CompleteConnect(this, true);
            uniqname = outpin.UniqName + "-" + inpin.UniqName;
        }


        public void Draw(Graphics g, bool selected, Point viewpoint)
        {
            bool pen_created = false;
            Pen pn;
            if (animation_state > 0) 
            {
                pn = new Pen(Color.FromArgb(animation_state*2, 150+animation_state/2, 0));
                pen_created = true;
            } else 
                pn = beigePen;
            Pen pen = selected ? yellowPen : pn;
            int cellsize = pins[0].Filter.cellsize;
            int mincx = 10000, maxcx = -10000, mincy = 10000, maxcy = -10000;
            foreach (ConStep cs in path)
            {
                int hc = cellsize / 2;
                int cell = cellsize;
                int cx = cs.x * cellsize + hc - viewpoint.X;
                int cy = cs.y * cellsize + hc - viewpoint.Y;
                switch (cs.dir)
                {
                    case 1: g.DrawLine(pen, cx, cy - hc, cx, cy + hc); break;
                    case 2: g.DrawLine(pen, cx - hc, cy, cx + hc, cy); break;
                    case 3: g.DrawArc(pen, cx, cy-cell, cell, cell, 90, 90); break;
                    case 4: g.DrawArc(pen, cx - cell, cy-cell, cell, cell, 0, 90); break;
                    case 5: g.DrawArc(pen, cx - cell, cy, cell, cell, 270, 90); break;
                    case 6: g.DrawArc(pen, cx, cy, cell, cell, 180, 90); break;
                }

                mincx = Math.Min(mincx, cx);
                maxcx = Math.Max(maxcx, cx);
                mincy = Math.Min(mincy, cy);
                maxcy = Math.Max(maxcy, cy);
            }
            mincx -= cellsize; mincy -= cellsize;
            maxcx += cellsize; maxcy += cellsize;
            last_drawn_rectangle = new Rectangle(mincx, mincy, maxcx - mincx, maxcy - mincy);
            if (pen_created) pn.Dispose();
        }

        public void Disconnect(bool really) //really==true - disconnect ipins
        {
            pins[0].CompleteConnect(null, really);
            pins[1].CompleteConnect(null, really);
            pins = null;
            path = null;
        }

        protected override bool IsSelected()
        {
            if (pins[0] == null || pins[0].Filter == null || pins[0].Filter.Graph == null) return false;
            return pins[0].Filter.Graph.SelectedConnection == this;
        }

        protected override void Redraw()
        {
            pins[0].Filter.Graph.Form.Invalidate(last_drawn_rectangle);
        }

        protected override bool IsHovered()
        {
            return pins[0].Filter.Graph.Form.HoveredConnectionID() == id;
        }
    }

}
