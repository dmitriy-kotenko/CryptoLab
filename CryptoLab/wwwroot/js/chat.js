var connection = new signalR.HubConnectionBuilder().withUrl('/chatHub').build();

var selectedUser;

connection.on('ClientConnected', appendUser);

connection.on('UserList', function (users) {
    users.forEach(appendUser);
});

connection.on('ClientDisconnected', function (userEmail) {
    var userLi = document.getElementById(userEmail);
    document.getElementById('usersList').removeChild(userLi);
});

connection.on('ReceiveMessage', function (message) {
    appendMessage(selectedUser, message);
});

connection.start();

$('#sendButton').click(e => {
    var messageInput = $('#messageInput');
    var message = messageInput.val();

    appendMessage('You', message);

    connection.invoke('SendMessage', selectedUser, message).catch(function (err) {
        return console.error(err.toString());
    });
    messageInput.val('');
    e.preventDefault();
});

function appendUser(userEmail) {
    var userItem = document.createElement('a');
    userItem.textContent = userEmail;
    userItem.href = '#';
    userItem.id = userEmail;
    userItem.className = 'list-group-item list-group-item-action list-group-item-light';
    userItem.setAttribute('data-toggle', 'tab');
    userItem.setAttribute('role', 'tab');
    userItem.onclick = onUserClick;
    document.getElementById('usersList').appendChild(userItem);
}

function appendMessage(user, message) {
    var element = document.createElement('p');
    element.textContent = user + ': ' + message;
    document.getElementById('messagesList').appendChild(element);
}

function onUserClick(e) {
    e.preventDefault();
    $(this).tab('show');
}

function onUserSelected(e) {
    selectedUser = e.target.id;
    document.getElementById('startConversation').disabled = false;
}


$('#startConversation').click(e => {
    $('#' + e.target.id).prop("disabled", true);
    $('#conversation').show();
});

$(document).ready(function () {
    $(document).on('shown.bs.tab', 'a[data-toggle="tab"]', onUserSelected);
});