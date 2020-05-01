// ==UserScript==
// @name         DeltaKendaxV2 Mode 1
// @namespace    deltakendax
// @version      2
// @match        https://www.habbo.com/*
// @run-at       document-start
// @description  Modo 1 para o Delta Kendax
// @author       Enemy
// @grant        GM_getValue
// @grant        GM_setValue
// ==/UserScript==

(function() {
    function appendContainer() {
        var s = "<style>#deltaKendax{z-index: 1000; background-color: #2e2e2e; width: 400px; height: 400px; position: absolute; left: 1%; top: 1%; border-bottom: 10px solid #242424; color: white;}";
        s+= "#deltaKendax #title{background-color: #242424; width: 100%; height: 50px; padding: 10px; box-sizing: border-box; }";
        s+= "#deltaKendax #content{width: 100%; height: 100%; padding: 20px; box-sizing: border-box;}";
        s+= "#deltaKendax textarea{width: 360px; height: 240px; resize: none; box-sizing: border-box; background-color: #2e2e2e; outline: none; color: yellow;}";
        s+= ".deltaBtn { background-color: #F33F3F; border: 0px solid black; border-radius: 5px; outline: none; padding: 10px; transition: all .3s ease-in;}";
        s+= ".deltaBtn:hover { background-color:#F33333;}";
        s+= "</style>";
        $("head").append(s);
        var h = "<div id='deltaKendax'><div id='title'>BonnieBot by Ric<br>Credits to Enemy</div>";
        h+= "<div id='content'><textarea id='accounts'></textarea>";
        h+= "<button class='deltaBtn' id='deltaStart'>Iniciar</button> <button class='deltaBtn' id='deltaStop'>Parar</button></div>";
        h+= "</div>";
        $("body").append(h);

        $("#deltaStart").click(() => {
            start();
        });

        $("#deltaStop").click(() => {
            stop();
        });
    }

    function start() {
        var lines = $("#accounts").val().split("\n");
        var accounts = [];
        for(var i = 0; i < lines.length; i++) {
            var credentials = lines[i].split(";");
            if(credentials[0] !== "")
                accounts[i] = {login: credentials[0], password: credentials[1]};
        }
        GM_setValue("accounts", accounts);
        GM_setValue("working", true);
        GM_setValue("index", 0);
        location.reload();
    }

    function stop() {
        GM_setValue("working", false);
    }

    function fillData() {
        var index = GM_getValue("index");
        var accounts = GM_getValue("accounts");

        document.title = "Logando [" + (index+1) + "/" + accounts.length + "]";


        if($(".header__login-form")){
            var account = accounts[index]
            $("[name='email']").val(account.login);
            $("[name='password']").val(account.password);
            //logar();
            login();
        } else {
            setTimeout(() => { fillData(); }, 2222);
        }
    }

    function wait() {
        if ($(".user-menu__header").length > 0)
            logout();
        else
            setTimeout(() => {
                wait();
            }, 5e2);
    }

    function login() {
        if ($(".login-form__button")) {
            $(".login-form__button").click();
            wait();
        }
        else
            setTimeout(() => {
                login();
            }, 1e3);
    }

    function logout(){
        window.location = "https://habbo.com/hotel";



        var index = GM_getValue("index");
        var accounts = GM_getValue("accounts");
        GM_setValue("index", index + 1);

        if(index + 1 == accounts.length)
                GM_setValue("working", false);

    }


    setTimeout(() => {
        appendContainer();
        if(GM_getValue("working")) {
            if(window.location.href.indexOf("hotel") > -1 || window.location.href.indexOf("client") > -1)
                window.location = "https://habbo.com/logout";
            else
                fillData();
        } else {
            if(window.location.href.indexOf("hotel") > -1 || window.location.href.indexOf("client") > -1) {
                window.location = "https://habbo.com/logout";
            }
        }
    }, 1e3);
})();