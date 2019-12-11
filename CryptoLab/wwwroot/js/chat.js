"use strict";

var connection = new signalR.HubConnectionBuilder().withUrl("/chatHub").build();

document.getElementById("sendButton").disabled = true;

connection.on("ClientConnected", function(userEmail) {
    var li = document.createElement("a");
    li.textContent = userEmail;
    li.id = userEmail;
    li.className = "list-group-item list-group-item-action";
    li.setAttribute("data-toggle", "tab");
    li.setAttribute("role", "tab");
    document.getElementById("usersList").appendChild(li);
});

connection.on("UserList", function(users) {
    users.forEach(user => {
        var li = document.createElement("a");
        li.textContent = user;
        li.id = user;
        li.className = "list-group-item list-group-item-action";
        li.setAttribute("data-toggle", "tab");
        li.setAttribute("role", "tab");
        document.getElementById("usersList").appendChild(li);
    });
});

connection.on("ClientDisconnected", function(userEmail) {
    var userLi = document.getElementById(userEmail);
    document.getElementById("usersList").removeChild(userLi);
});

connection.on("ReceiveMessage", function(message) {
    var msg = message.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
    var li = document.createElement("li");
    li.textContent = msg;
    document.getElementById("messagesList").appendChild(li);
});

connection.start().then(function() {
    document.getElementById("sendButton").disabled = false;
}).catch(function(err) {
    return console.error(err.toString());
});

document.getElementById("sendButton").addEventListener("click", function(event) {
    var user = document.getElementById("userInput").value;
    var message = document.getElementById("messageInput").value;
    connection.invoke("SendMessage", user, message).catch(function(err) {
        return console.error(err.toString());
    });
    event.preventDefault();
});

$('#usersList a').on('click', function(e) {
    console.log("aa");
    e.preventDefault();
    $(this).tab('show');
})