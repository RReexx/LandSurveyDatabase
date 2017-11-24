using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using ESRI.ArcGIS.AnalysisTools;
using ESRI.ArcGIS.Carto;
using ESRI.ArcGIS.CatalogUI;
using ESRI.ArcGIS.Controls;
using ESRI.ArcGIS.DataSourcesFile;
using ESRI.ArcGIS.Display;
using ESRI.ArcGIS.esriSystem;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.Geometry;
using ESRI.ArcGIS.Geoprocessing;
using ESRI.ArcGIS.SystemUI;
using ESRI.ArcGIS.DataManagementTools;

namespace LSD.Edit.Forms
{
    public partial class AppendForm : Form
    {
        private IMapControl3 mapcontrol;
        IMap m_map;
        IFeatureClass targetClass,sourceClass;//编辑属性用的
        IWorkspaceEdit m_WorkspaceEdit;        
        
        public AppendForm(IMapControl3 axMapCtrl)
        {
            mapcontrol = axMapCtrl;
            InitializeComponent();
        }

        private void AppendForm_Load(object sender, EventArgs e)
        {
            //把地图控件和地图连接好            
            m_map = mapcontrol.Map;
            //加载当前地图中的所有图层
            addLayer();
        }

        #region 一些辅助用的函数
        //把地图中现有的图层填充到combobox
        private void addLayer()
        {
            List<ILayer> plstLayers = LSD.Edit.EditTool.BasicClass.MapManager.GetLayers(m_map);
            InitComboBox(plstLayers);
        }

        private void InitComboBox(List<ILayer> plstLyr)
        {
            cboTarget.Items.Clear();
            for (int i = 0; i < plstLyr.Count; i++)
            {
                if (!cboTarget.Items.Contains(plstLyr[i].Name))
                {
                    cboTarget.Items.Add(plstLyr[i].Name);
                }
            }
            if (cboTarget.Items.Count != 0) cboTarget.SelectedIndex = 0;
        }

        private IEnumLayer GetLayers()
        {
            UID uid = new UIDClass();
            uid.Value = "{E156D7E5-22AF-11D3-9F99-00C04F6BC78E}";// IFeatureLayer//E156D7E5-22AF-11D3-9F99-00C04F6BC78E
            //uid.Value = "{E156D7E5-22AF-11D3-9F99-00C04F6BC78E}";  // IGeoFeatureLayer40A9E885-5533-11d0-98BE-00805F7CED21
            //uid.Value = "{6CA416B1-E160-11D2-9F4E-00C04F6BC78E}";  // IDataLayer
            if (m_map.LayerCount != 0)
            {
                IEnumLayer layers = m_map.get_Layers(uid, true);
                return layers;
            }
            return null;
        }

        private IFeatureLayer GetFeatureLayer(string layerName)
        {

            if (GetLayers() == null) return null;
            IEnumLayer layers = GetLayers();
            layers.Reset();

            ILayer layer = null;
            while ((layer = layers.Next()) != null)
            {
                if (layer.Name == layerName)
                    return layer as IFeatureLayer;
            }
            return null;
        }
        #endregion

        private void btnAdd_Click(object sender, EventArgs e)
        {       

            //判断是否选择要素
            if (this.txtInput.Text == "" || this.txtInput.Text == null)
            {
                txtMessage.Text = "请设置输入要素！";
                return;
            }
            if (this.cboTarget.SelectedItem==null)
            {
                txtMessage.Text = "请选择目标要素！";
                return;
            }

            if (this.txtInput.Text.EndsWith("shp", true, null))
            {
                AppendShp();//追加shp，如果还有其他格式就再else if
            }            
            else
            {
                txtMessage.Text = "请输入正确的矢量数据类型！需为shp格式的数据。";
                return;
            } 
        }

        private void AppendShp() 
        {
            try 
            {
                //读
                List<IGeometry> geometries = GetShpGeometriesFromFile(txtInput.Text);
                if (geometries == null) return;
                //写
                EditShpShape(geometries);
            }
            catch(Exception e)
            {
                txtMessage.Text += "导入shp格式地块出现错误" + e.Message;
            }                        
        }   

        private void btnInput_Click(object sender, EventArgs e)
        {
            //定义OpenfileDialog
            OpenFileDialog openDlg = new OpenFileDialog();
            openDlg.Filter = "Shapefile (*.shp)|*.shp|AutoCAD 图形(*.dwg)|*.dwg|All Files(*.*)|*.*";//之后还要加上E00，Coverage
            openDlg.Title = "选择要导入的要素";
            //检验文件和路径是否存在
            openDlg.CheckFileExists = true;
            openDlg.CheckPathExists = true;
            //初试化初试打开路径
            openDlg.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);            
            //读取文件路径到txtFeature1中
            if (openDlg.ShowDialog() == DialogResult.OK)
            {
                this.txtInput.Text = openDlg.FileName;
            }
        }

        public List<IGeometry> GetShpGeometriesFromFile(string filePath)
        {
            IWorkspaceFactory pWorkspaceFactory;
            IFeatureWorkspace pFeatureWorkspace;
            IFeatureClass pFeatureClass;
            
            List<IGeometry> geometries = new List<IGeometry>();
            IGeometry ge = null;
            try
            {
                string workspacePath = System.IO.Path.GetDirectoryName(filePath);
                string fileName = System.IO.Path.GetFileName(filePath);

                //打开数据集
                pWorkspaceFactory = new ShapefileWorkspaceFactoryClass();
                pFeatureWorkspace = (IFeatureWorkspace)pWorkspaceFactory.OpenFromFile(workspacePath, 0);
                //打开一个要素类
                pFeatureClass = pFeatureWorkspace.OpenFeatureClass(fileName);
                sourceClass = pFeatureClass;//这是给编辑属性的函数用的全局变量
                IFeatureCursor pFeatureCursor;
                IFeature fe = null;
                //把源要素类的所有要素的图形都存在geometries列表里
                pFeatureCursor = pFeatureClass.Search(null, false);
                fe = pFeatureCursor.NextFeature();
                while (fe != null)
                {
                    ge = fe.Shape as IGeometry;
                    geometries.Add(ge);
                    fe = pFeatureCursor.NextFeature();
                }
            }
            catch (Exception ex)
            {
                throw;
            }
            return geometries;
        }

        //把图形追加到目标要素，shp的话对应的属性也加进去了
        private void EditShpShape(List<IGeometry> geometries)
        {
            //这两个是目标要素
            IFeatureLayer fl = GetFeatureLayer(cboTarget.Text);
            IFeatureClass fc = fl.FeatureClass;
            
            targetClass = fc;//这是给编辑属性用的全局变量
            //获取目标要素和源要素共同的字段
            List<string> samefields = GetSameFields(targetClass, sourceClass);           
            
            //追加是在编辑流程中
            m_WorkspaceEdit = (fc as IDataset).Workspace as IWorkspaceEdit;
            m_WorkspaceEdit.StartEditing(true);
            m_WorkspaceEdit.StartEditOperation();
            
            int sourceIndex = 0;//处理到第几个要素了，表示目标和源的属性表的行

            //对每个图形，在目标要素类中创建一个要素，把图形和对应的属性赋过去
            foreach (IGeometry g in geometries)
            {
                IFeature p_Feature = fc.CreateFeature();
                p_Feature.Shape = g;                
                //把源要素类中对应那个要素的对应的属性赋过去               
                EditProperty(p_Feature, sourceIndex, samefields);                              
                p_Feature.Store();
                sourceIndex++;
                txtMessage.Text += "正在添加第" + sourceIndex + "个地块" + "\r\n";
            }
            
            m_WorkspaceEdit.StopEditOperation();
            //m_WorkspaceEdit.StopEditing(true);//先不保存 TODO最后还是要改成保存的 
            
            IEnvelope envelope = ComputeEnvelope(geometries);
            this.mapcontrol.ActiveView.Extent = envelope;//缩放到追加的那些要素的范围，这个可有可没有
            this.mapcontrol.ActiveView.PartialRefresh(esriViewDrawPhase.esriViewGeography, null, envelope);
            txtMessage.Text += "添加图形和属性至目标图层成功！已填入与目标图层字段一致的属性。已计算并填入与面积相关的属性值。" + "\r\n";
        }

        //计算添加进来的地块的范围，添加后缩放到此范围，也可以局部刷新的时候用
        private IEnvelope ComputeEnvelope(List<IGeometry> ge)
        {
            double xmax, xmin, ymax, ymin;
            xmax = ge[0].Envelope.XMax;
            xmin = ge[0].Envelope.XMin;
            ymax = ge[0].Envelope.YMax;
            ymin = ge[0].Envelope.YMin;
            for (int i = 1; i < ge.Count; i++)
            {
                if (ge[i].Envelope.XMax > xmax)
                    xmax = ge[i].Envelope.XMax;
                if (ge[i].Envelope.XMin < xmin)
                    xmin = ge[i].Envelope.XMin;
                if (ge[i].Envelope.YMax > ymax)
                    ymax = ge[i].Envelope.YMax;
                if (ge[i].Envelope.YMin < ymin)
                    ymin = ge[i].Envelope.YMin;
            }
            IEnvelope enve = new EnvelopeClass() as IEnvelope;
            enve.PutCoords(xmin, ymin, xmax, ymax);
            return enve;
        }
        
        //获取源要素类和目标要素类中字段名字和类型相同的那些字段。复杂度是n方，但是字段个数不可能非常多，可用
        private List<string> GetSameFields(IFeatureClass target, IFeatureClass source) 
        {
            List<string> sameFields = new List<string>();//用来保存目标与源图层所有一致的字段            
            int sFieldCnt = source.Fields.FieldCount;//字段个数
            int tFieldCnt = target.Fields.FieldCount;
            //遍历源要素类的所有字段
            for (int i = 0; i < sFieldCnt; i++)
            {
                string sfName = source.Fields.get_Field(i).Name;//源要素类字段的名字
                string sftypeName = source.Fields.get_Field(i).Type.ToString();//源要素类字段的类型
                //在目标要素中找，是否有名字和类型都一样的
                for (int j = 0; j < tFieldCnt; j++)
                {
                    string tfName = target.Fields.get_Field(j).Name;
                    string tftypeName = target.Fields.get_Field(j).Type.ToString();
                    bool editable = target.Fields.get_Field(j).Editable;//有些字段是不可编辑的，比如FID，shape，长度面积等
                    if (sfName.Equals(tfName) && sftypeName.Equals(tftypeName) && editable)//字段名字相同且类型相同且可编辑                       
                        sameFields.Add(tfName);//想要的
                }
            }
            return sameFields;
        }
        
        /// <summary>
        /// 根据指定的要素和字段编辑属性
        /// </summary>
        /// <param name="feature">对应新创建的那个要素</param>
        /// <param name="count">表示要素类中的第几个要素</param>
        /// <param name="fields">要编辑的那些字段</param>
        private void EditProperty(IFeature feature, int count, List<string> fields) 
        {            
            IFeature feSource = sourceClass.GetFeature(count);
            foreach (string aField in fields)//每一列
            {
                object value = feSource.get_Value(feSource.Fields.FindField(aField));
                feature.set_Value(feature.Fields.FindField(aField), value);
                feature.Store();
            }            
        }
    }
}
