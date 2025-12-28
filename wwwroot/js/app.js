// 全局变量
let currentUser = null;
let currentToken = null;

// DOM加载完成后执行
document.addEventListener('DOMContentLoaded', function() {
    // 初始化代码编辑器
    initCodeEditors();
    
    // 绑定事件
    bindEvents();
    
    // 检查是否已登录
    checkLoginStatus();
});

// 初始化代码编辑器
function initCodeEditors() {
    require.config({ paths: { 'vs': 'https://cdn.jsdelivr.net/npm/monaco-editor@0.40.0/min/vs' }});
    
    require(['vs/editor/editor.main'], function() {
        // 查询编辑器
        window.queryEditor = monaco.editor.create(document.getElementById('query-editor'), {
            value: '{\n  "dbId": "",\n  "table": "",\n  "operation": "select",\n  "where": {},\n  "orderBy": {},\n  "page": {\n    "index": 1,\n    "size": 10\n  },\n  "columns": [],\n  "data": {},\n  "joins": []\n}',
            language: 'json',
            theme: 'vs-light',
            automaticLayout: true,
            minimap: { enabled: false },
            scrollBeyondLastLine: false
        });
        
        // 结果编辑器
        window.resultEditor = monaco.editor.create(document.getElementById('result-editor'), {
            value: '{\n  "success": false,\n  "message": "请执行查询以查看结果",\n  "data": null,\n  "total": 0\n}',
            language: 'json',
            theme: 'vs-light',
            readOnly: true,
            automaticLayout: true,
            minimap: { enabled: false },
            scrollBeyondLastLine: false
        });
    });
}

// 绑定事件
function bindEvents() {
    // 登录按钮
    document.getElementById('login-btn').addEventListener('click', handleLogin);
    
    // 退出按钮
    document.getElementById('logout-btn').addEventListener('click', handleLogout);
    
    // 菜单切换
    document.getElementById('menu-toggle').addEventListener('click', toggleSidebar);
    
    // 侧边栏导航
    document.querySelectorAll('.sidebar-nav li').forEach(item => {
        item.addEventListener('click', function() {
            switchPage(this.getAttribute('data-page'));
        });
    });
    
    // 数据库选择变化
    document.getElementById('database-select').addEventListener('change', loadTables);
    
    // 执行按钮
    document.getElementById('execute-btn').addEventListener('click', executeQuery);
    
    // 清除按钮
    document.getElementById('clear-btn').addEventListener('click', clearQuery);
    
    // 添加连接按钮
    document.getElementById('add-connection-btn').addEventListener('click', showAddConnectionDialog);
    
    // 测试连接按钮
    document.getElementById('test-connection-btn').addEventListener('click', testConnection);
    
    // 保存连接按钮
    document.getElementById('save-connection-btn').addEventListener('click', saveConnection);
    
    // 对话框取消按钮
    document.querySelectorAll('.dialog-cancel').forEach(btn => {
        btn.addEventListener('click', hideDialog);
    });
    
    // 对话框关闭按钮
    document.querySelectorAll('.dialog-close').forEach(btn => {
        btn.addEventListener('click', hideDialog);
    });
}

// 检查登录状态
function checkLoginStatus() {
    const token = localStorage.getItem('token');
    if (token) {
        // 验证令牌
        axios.post('/api/auth/validate-token', { token })
            .then(response => {
                if (response.data.Success) {
                    currentToken = token;
                    currentUser = response.data.UserId;
                    showApp();
                } else {
                    localStorage.removeItem('token');
                }
            })
            .catch(error => {
                console.error('验证令牌失败:', error);
                localStorage.removeItem('token');
            });
    }
}

// 处理登录
function handleLogin() {
    const username = document.getElementById('username').value;
    const password = document.getElementById('password').value;
    const errorElement = document.getElementById('login-error');
    
    if (!username || !password) {
        errorElement.textContent = '用户名和密码不能为空';
        return;
    }
    
    axios.post('/api/auth/login', { username, password })
        .then(response => {
            if (response.data.Success) {
                currentToken = response.data.Token;
                currentUser = response.data.UserId;
                localStorage.setItem('token', currentToken);
                showApp();
            } else {
                errorElement.textContent = response.data.Message || '登录失败';
            }
        })
        .catch(error => {
            console.error('登录失败:', error);
            errorElement.textContent = '登录失败，请稍后重试';
        });
}

// 处理退出
function handleLogout() {
    localStorage.removeItem('token');
    currentToken = null;
    currentUser = null;
    hideApp();
}

// 显示应用界面
function showApp() {
    document.getElementById('login-container').style.display = 'none';
    document.getElementById('app-container').style.display = 'block';
    document.getElementById('current-user').textContent = currentUser;
    
    // 加载数据库列表
    loadDatabases();
}

// 隐藏应用界面
function hideApp() {
    document.getElementById('login-container').style.display = 'flex';
    document.getElementById('app-container').style.display = 'none';
    document.getElementById('username').value = '';
    document.getElementById('password').value = '';
    document.getElementById('login-error').textContent = '';
}

// 切换侧边栏
function toggleSidebar() {
    document.getElementById('app-sidebar').classList.toggle('active');
}

// 切换页面
function switchPage(pageId) {
    // 更新导航菜单
    document.querySelectorAll('.sidebar-nav li').forEach(item => {
        item.classList.remove('active');
    });
    document.querySelector(`.sidebar-nav li[data-page="${pageId}"]`).classList.add('active');
    
    // 显示对应页面
    document.querySelectorAll('.page').forEach(page => {
        page.style.display = 'none';
    });
    document.getElementById(`${pageId}-page`).style.display = 'block';
}

// 加载数据库列表
function loadDatabases() {
    axios.get('/api/api-test/databases', {
        headers: { 'Authorization': `Bearer ${currentToken}` }
    })
    .then(response => {
        const select = document.getElementById('database-select');
        select.innerHTML = '<option value="">-- 请选择数据库 --</option>';
        
        if (response.data.Success && response.data.Data) {
            response.data.Data.forEach(db => {
                const option = document.createElement('option');
                option.value = db.Id;
                option.textContent = `${db.Name} (${db.Type})`;
                select.appendChild(option);
            });
        }
    })
    .catch(error => {
        console.error('加载数据库列表失败:', error);
        alert('加载数据库列表失败');
    });
}

// 加载表列表
function loadTables() {
    const dbId = document.getElementById('database-select').value;
    if (!dbId) return;
    
    axios.get('/api/api-test/tables', {
        params: { databaseId: dbId },
        headers: { 'Authorization': `Bearer ${currentToken}` }
    })
    .then(response => {
        const select = document.getElementById('table-select');
        select.innerHTML = '<option value="">-- 请选择表 --</option>';
        
        if (response.data.Success && response.data.Data) {
            response.data.Data.forEach(table => {
                const option = document.createElement('option');
                option.value = table.Name;
                option.textContent = table.Name;
                select.appendChild(option);
            });
        }
    })
    .catch(error => {
        console.error('加载表列表失败:', error);
        alert('加载表列表失败');
    });
}

// 执行查询
function executeQuery() {
    const query = window.queryEditor.getValue();
    
    try {
        const queryObj = JSON.parse(query);
        
        axios.post('/api/api-test/test-query', queryObj, {
            headers: { 'Authorization': `Bearer ${currentToken}` }
        })
        .then(response => {
            window.resultEditor.setValue(JSON.stringify(response.data, null, 2));
        })
        .catch(error => {
            console.error('执行查询失败:', error);
            window.resultEditor.setValue(JSON.stringify({
                success: false,
                message: error.response?.data?.Message || '执行查询失败',
                data: null,
                total: 0
            }, null, 2));
        });
    } catch (e) {
        window.resultEditor.setValue(JSON.stringify({
            success: false,
            message: 'JSON格式错误: ' + e.message,
            data: null,
            total: 0
        }, null, 2));
    }
}

// 清除查询
function clearQuery() {
    window.queryEditor.setValue('{\n  "dbId": "",\n  "table": "",\n  "operation": "select",\n  "where": {},\n  "orderBy": {},\n  "page": {\n    "index": 1,\n    "size": 10\n  },\n  "columns": [],\n  "data": {},\n  "joins": []\n}');
    window.resultEditor.setValue('{\n  "success": false,\n  "message": "请执行查询以查看结果",\n  "data": null,\n  "total": 0\n}');
}

// 显示添加连接对话框
function showAddConnectionDialog() {
    document.getElementById('connection-dialog-title').textContent = '添加数据库连接';
    document.getElementById('connection-id').value = '';
    document.getElementById('connection-name').value = '';
    document.getElementById('connection-type').value = 'SqlServer';
    document.getElementById('connection-string').value = '';
    document.getElementById('connection-default').checked = false;
    document.getElementById('connection-dialog').style.display = 'flex';
}

// 测试连接
function testConnection() {
    const config = {
        Id: document.getElementById('connection-id').value,
        Name: document.getElementById('connection-name').value,
        Type: document.getElementById('connection-type').value,
        ConnectionString: document.getElementById('connection-string').value,
        IsDefault: document.getElementById('connection-default').checked
    };
    
    axios.post('/api/database-connection/test', config, {
        headers: { 'Authorization': `Bearer ${currentToken}` }
    })
    .then(response => {
        alert(response.data.Message);
    })
    .catch(error => {
        alert(error.response?.data?.Message || '测试连接失败');
    });
}

// 保存连接
function saveConnection() {
    const config = {
        Id: document.getElementById('connection-id').value,
        Name: document.getElementById('connection-name').value,
        Type: document.getElementById('connection-type').value,
        ConnectionString: document.getElementById('connection-string').value,
        IsDefault: document.getElementById('connection-default').checked
    };
    
    axios.post('/api/database-connection', config, {
        headers: { 'Authorization': `Bearer ${currentToken}` }
    })
    .then(response => {
        if (response.data.Success) {
            alert('保存成功');
            hideDialog();
            loadDatabases();
        } else {
            alert(response.data.Message || '保存失败');
        }
    })
    .catch(error => {
        alert(error.response?.data?.Message || '保存失败');
    });
}

// 隐藏对话框
function hideDialog() {
    document.querySelectorAll('.dialog').forEach(dialog => {
        dialog.style.display = 'none';
    });
}