
namespace Microsoft.AspNetCore.Mvc
{
    using WebSample;
    using System;
    using System.IO;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using System.Text.Encodings.Web;
    public abstract class View<TModel> : IView//No TagHelper,Layout
    {
        #region private
        private TModel _model;
        private IDictionary<string, object> _viewData;
        private IDictionary<string, Func<Task>> _sections;
        private IDictionary<string, Func<Task>> _defineSections;
        private TextWriter _output;
        private ViewEngine _engine;
        #endregion
        public TModel Model
        {
            get => _model;
            set => _model = value;
        }
        public IDictionary<string, object> ViewData 
        {
            get 
            {
                if (_viewData == null)
                    _viewData = new Dictionary<string, object>();
                return _viewData;
            }
            set 
            {
                _viewData = value;
            }
        }

        #region IView
        object IView.Model
        {
            get => _model;
            set => _model = (TModel)value;
        }
        IDictionary<string, Func<Task>> IView.Sections
        {
            get => _sections;
            set => _sections = value;
        }
        TextWriter IView.Output
        {
            get => _output;
            set => _output = value;
        }
        ViewEngine IView.Engine
        {
            get => _engine;
            set => _engine = value;
        }
        #endregion
        public abstract Task ExecuteAsync();
        public object Render(string viewName, object model)
        {
            var view = _engine.Create(viewName);
            if (view == null)
                throw new KeyNotFoundException(nameof(viewName));

            view.Model = model;
            view.ViewData = _viewData;
            view.Output = _output;
            view.Engine = _engine;
            view.Sections = _defineSections;
            view.ExecuteAsync().Wait();
            return null;
        }
        public object Render(string viewName, object model, IDictionary<string, object> viewData) 
        {
            var view = _engine.Create(viewName);
            if (view == null)
                throw new KeyNotFoundException(nameof(viewName));

            view.Model = model;
            view.ViewData = viewData;
            view.Output = _output;
            view.Engine = _engine;
            view.Sections = _defineSections;
            view.ExecuteAsync().Wait();
            return null;
        }
        public async Task<object> RenderAsync(string viewName, object model)
        {
            var view = _engine.Create(viewName);
            if (view == null)
                throw new KeyNotFoundException(nameof(viewName));

            view.Model = model;
            view.ViewData = _viewData;
            view.Output = _output;
            view.Engine = _engine;
            view.Sections = _defineSections;
            await view.ExecuteAsync();
            return null;
        }
        public async Task<object> RenderAsync(string viewName, object model, IDictionary<string, object> viewData)
        {
            var view = _engine.Create(viewName);
            if (view == null)
                throw new KeyNotFoundException(nameof(viewName));

            view.Model = model;
            view.ViewData = viewData;
            view.Output = _output;
            view.Engine = _engine;
            view.Sections = _defineSections;
            await view.ExecuteAsync();
            return null;
        }
        public void DefineSection(string name, Func<Task> section)
        {
            Debug.Assert(name != null);
            if (_defineSections == null)
                _defineSections = new Dictionary<string, Func<Task>>();

            _defineSections.Add(name, section);
        }
        public bool IsSectionDefined(string name)
        {
            if (_defineSections != null && _defineSections.ContainsKey(name))
                return true;
            if (_sections != null && _sections.ContainsKey(name))
                return true;
            return false;
        }
        public object RenderSection(string name)
        {
            if (_defineSections != null && _defineSections.TryGetValue(name,out var section))
            {
                section().Wait();
            }
            else if (_sections != null && _sections.TryGetValue(name,out section))
            {
                section().Wait();
            }
            return null;
        }
        public async Task<object> RenderSectionAsync(string name)
        {
            if (_defineSections != null && _defineSections.TryGetValue(name, out var section))
            {
                await section();
            }
            else if (_sections != null && _sections.TryGetValue(name, out section))
            {
                await section();
            }
            return null;
        }
        public RawString Raw(string value)
        {
            return new RawString(value);
        }
        public void WriteLiteral(string value)
        {
            _output.Write(value);
        }
        public void Write(RawString value)
        {
            _output.Write(value.Value);
        }
        public void Write(int value) 
        {
            _output.Write(value);
        }
        public void Write(long value) 
        {
            _output.Write(value);
        }
        public void Write(DateTime value) 
        {
            const string format = "yyyy-MM-dd HH:mm:ss";
            Span<char> charSpan = stackalloc char[format.Length];
            if (value.TryFormat(charSpan, out var charsWritten, format))
            {
                Debug.Assert(charsWritten == format.Length);
                _output.Write(charSpan);//use Write()?
            }
            else 
            {
                _output.Write(value.ToString(format));
            }
        }
        public void Write(string value)
        {
            if (value != null)
                HtmlEncoder.Default.Encode(_output, value);
        }
        public void Write(object value)
        {
            if (value == null)
                return;

            Write(value.ToString());
        }
        #region Attribute
        private string _suffix;
        public void BeginWriteAttribute(string name, string prefix, int prefixOffset, string suffix, int suffixOffset, int attributeValuesCount)
        {
            _output.Write(prefix);
            _suffix = suffix;
        }
        public void WriteAttributeValue(string prefix, int prefixOffset, string value, int valueOffset, int valueLength, bool isLiteral)
        {
            if (isLiteral)
                _output.Write(value);
            else
                Write(value);
        }
        public void WriteAttributeValue(string prefix,int prefixOffset,object value,int valueOffset,int valueLength,bool isLiteral)
        {
            Debug.Assert(!isLiteral);
            Write(value);
        }
        public void EndWriteAttribute()
        {
            if (_suffix.Length == 1)
                _output.Write(_suffix[0]);
            else
                _output.Write(_suffix);
        }
        #endregion
        //DesignTime
        [EditorBrowsable(EditorBrowsableState.Never)]//Not Use
        public void DefineSection(string name, Func<object, Task> section)
        {
            throw new NotImplementedException(name);
        }

        //TODO? PushWriter PopWriter
    }
}