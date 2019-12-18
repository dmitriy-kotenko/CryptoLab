window.generateRSAKeyAndStore = function () {
	var NodeRSA = require('node-rsa');
	var key = new NodeRSA({b: 512});
	localStorage.setItem("prvKey", key.exportKey("pkcs8-private"));
	localStorage.setItem("pubKey", key.exportKey("public"));
	return key;
};

window.encryptWithRSA = function (key, text) {
	var NodeRSA = require('node-rsa');
	var key = new NodeRSA(key);
	key.setOptions({encryptionScheme: 'pkcs1'});

	return key.encrypt(text, 'base64');	
};

window.decryptWithRSA = function (key, text) {
	var NodeRSA = require('node-rsa');
	var key = new NodeRSA(key);
	key.setOptions({encryptionScheme: 'pkcs1'});
	
	return key.decrypt(text);	
};

window.verifyWithRSA = function (key, data, signature) {
	var NodeRSA = require('node-rsa');
	var key = new NodeRSA(key);
	key.setOptions({signingScheme: 'pkcs1'});
	
	return key.verify(data, signature, 'base64', 'base64');	
};