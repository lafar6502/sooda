<?php
include "_layout.php";
write_start_page();
?>
		<h1>Wprowadzenie</h1>
		<p>
		System <b>Sooda</b> (Simple Object-Oriented Data Access) pozwala na automatyczne generowanie 
		obiektowej warstwy dost�pu do danych (Data Access Layer) dla aplikacji dzia�aj�cych
		na platformie .NET.
		</p>
		<p>
		Sooda jest oprogramowaniem open source dystrybuowanym 
        na zasadach <a href="../LICENSE.txt">licencji BSD</a>. Narz�dzie <b>SoodaQuery</b> dystrybuowane jest
        na zasadach licencji <a href="../SoodaQuery_LICENSE.txt">GPL v2</a>, poniewa� wykorzystuje komponent 
        <a href="http://www.icsharpcode.net/OpenSource/SD/Default.aspx">ICSharpCode.TextEditor.dll</a>.
		</p>
		<h2>Mo�liwo�ci systemu</h2>
		<p>
		System udost�pnia nast�puj�ce mechanizmy wspieraj�ce tworzenie rozwi�za� bazodanowych:</p>
		<ul>
			<li>automatyczne materializowanie obiekt�w bazodanowych jako obiekt�w w �rodowisku zarz�dzanym</li>
			<li>obs�uga wszystkich prostych typ�w danych wspieranych przez CLI + mo�liwo�� definiowania w�asnych typ�w i sposob�w odwzorowania</li>
			<li>naturalne odwzorowanie relacji 1-N w formie obiekt�w powi�zanych i kolekcji</li>
			<li>naturalne odwzorowanie relacji N-N w formie kolekcji obiekt�w</li>
			<li>przezroczysty dost�p do heterogenicznych �r�de� danych (RDBMS, pliki XML, katalogi LDAP i ActiveDirectory)</li>
			<li>serializacja i deserializacja do formatu XML</li>
			<li>optymistyczne blokowanie i wersjonowanie obiekt�w w pami�ci</li>
			<li>wsparcie dla d�ugo dzia�aj�cych transakcji (<i>long running transactions</i>)</li>
		</ul>
		<p>Sooda wspiera <a href="http://msdn.microsoft.com/net">Microsoft .NET Framework</a> w wersji 1.x, mo�liwa jest tak�e kompilacja 
		z u�yciem <a href="http://www.go-mono.com">Mono</a>, lecz nie wszystkie funkcjonalno�ci
		dzia�aj� w tym �rodowisku.</p>
<?php
write_end_page();
?>			
