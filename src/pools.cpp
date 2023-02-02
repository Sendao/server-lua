#include "main.h"

unordered_map<size_t, vector<void*>*> pools;
StringMemory *strmem;

void init_pools( void )
{
	pools.reserve( 1000 );
	
	strmem = (StringMemory*)halloc( sizeof(StringMemory) );
	new(strmem) StringMemory();
}

void *halloc( size_t sz )
{
	unordered_map<size_t, vector<void*>*>::iterator it;
	vector <void*> *pool;
	if( (it=pools.find(sz)) != pools.end() ) {
		pool = it->second;
		if( pool->size() > 0 ) {
			void *ptr = pool->back();
			pool->pop_back();
			return ptr;
		}
	}
	return malloc(sz);
}
void hfree( void *ptr, size_t sz )
{
	unordered_map<size_t, vector<void*>*>::iterator it;
	vector <void*> *pool;
	if( (it=pools.find(sz)) != pools.end() ) {
		pool = it->second;
		pool->push_back(ptr);
	} else {
		pool = new vector<void*>();
		pool->push_back(ptr);
		pools[sz] = pool;
	}
}

StringMemory::StringMemory()
{
	size_t size = 1024*1024;
	char *zero = (char*)malloc( size );

	StringMemoryItem sptr(zero, size);
	StringMemoryItem2 ssz(zero, size);

	items_ptr.insert( sptr );
	items_sz.insert( ssz );
}

StringMemory::~StringMemory()
{
	set<StringMemoryItem>::iterator it;
	for( it=items_ptr.begin(); it!=items_ptr.end(); it++ ) {
		const StringMemoryItem &x = *it;
		free( (void*)x.ptr );
	}
}


StringMemoryItem::StringMemoryItem( const StringMemoryItem2 &a )
: ptr(a.ptr), size(a.size)
{
//	lprintf("Built %p from %p", ptr, a.ptr);
}

char *StringMemory::Realloc( char *ptr, size_t orig_sz, size_t new_sz )
{
	char *np = Alloc( new_sz );

	if( orig_sz != 0 ) {
		memcpy( np, ptr, orig_sz );
		Free( ptr, orig_sz );
	}

	return np;
}
char *StringMemory::ReallocStr( char *ptr, size_t orig_sz, size_t new_sz )
{
	char *np = Alloc( new_sz );

	if( orig_sz != 0 ) {
		strcpy( np, ptr );
		Free( ptr, orig_sz );
	}

	return np;
}

char *StringMemory::Alloc( size_t sz )
{
	set<StringMemoryItem2>::iterator it;
	char *ptr;
	unsigned int sz1, sz2;

	sz1 = (unsigned int) items_sz.size();
	//lprintf("Alloc: Searching in %u blocks", sz1);

	it = items_sz.lower_bound( StringMemoryItem2(NULL, sz) );
	if( it != items_sz.end() ) {
		const StringMemoryItem2 &item = *it;
		sz1 = (unsigned int)item.size;
		sz2 = (unsigned int)sz;
		//lprintf("Found matching memory block %p of size %u looking for %u", item.ptr, sz1, sz2);
		StringMemoryItem2 newitem;
		StringMemoryItem item1;
		set<StringMemoryItem>::iterator it1;

		ptr = item.ptr;
		item1 = StringMemoryItem(item);
		items_sz.erase( it );
		sz1 = (unsigned int)item1.size;
		it1 = items_ptr.find(item1);
		if( it1 == items_ptr.end() ) {
			lprintf("Error: unmatched memory");
			abort();
		}
		if( item1.size > sz ) {
			newitem.ptr = item1.ptr + sz;
			newitem.size = item1.size - sz;
			items_sz.insert( newitem );
			items_ptr.insert( StringMemoryItem(newitem) );
		}
		items_ptr.erase( it1 );
		return ptr;
	}
	
	// no free block found, allocate new
	ptr = (char*)malloc( sz );
	StringMemoryItem sptr(ptr, sz);
	StringMemoryItem2 ssz(ptr, sz);
	items_ptr.insert( sptr );
	items_sz.insert( ssz );
	return ptr;
}

void StringMemory::Free( char *ptr, size_t sz )
{
	set<StringMemoryItem>::iterator it;
	StringMemoryItem srch(ptr, sz);

	unsigned int sz1 = (unsigned int)sz;
	//lprintf("Release %u bytes", sz1);
	
	it = items_ptr.lower_bound( srch );
	if( it != items_ptr.end() && it != items_ptr.begin() ) {
		const StringMemoryItem &item = *it;
		if( ptr + sz == item.ptr ) {
			StringMemoryItem newitem(item);

			items_sz.erase( items_sz.find( item ) );
			items_ptr.erase( it );

			newitem.ptr = ptr;
			newitem.size += sz;
			items_sz.insert( StringMemoryItem2(newitem) );
			items_ptr.insert( newitem );
			return;
		}
		it--;
		const StringMemoryItem &itemb = *it;
		if( itemb.ptr + itemb.size == ptr ) {
			StringMemoryItem newitem(itemb);

			items_sz.erase( items_sz.find( itemb ) );
			items_ptr.erase( it );

			newitem.size += sz;
			items_sz.insert( StringMemoryItem2(newitem) );
			items_ptr.insert( newitem );
			return;
		}
	}
	items_ptr.insert( srch );
	items_sz.insert( StringMemoryItem2(srch) );
}

/*
StringTrie::StringTrie()
{
	str = NULL;
	for( int i=0; i<27; i++ ) {
		t[i] = NULL;
	}
}

StringTrie::~StringTrie()
{
	for( int i=0; i<27; i++ ) {
		if( t[i] ) {
			delete t[i];
		}
	}
}

StringTrie::Add(const char *str, int offset)
{
	int i, c;
	const char *ptr;
	StringTrie *st = this;

	for( ptr = str+offset; ptr; ptr++ ) {
		c = *ptr;
		if( c >= 'a' && c <= 'z' ) {
			c -= 'a';
		} else if( c >= 'A' && c <= 'Z' ) {
			c -= 'A';
		} else {
			c = 26;
		}
		if( !st->t[c] ) {
			st->t[c] = new StringTrie();
			st->t[c]->str = str;
			st = t[c];
		}
	}
}
*/
